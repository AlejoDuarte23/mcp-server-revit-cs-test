import json
import logging
import os
import time
from abc import ABC
from abc import abstractmethod
from typing import Any

import requests
from dotenv import load_dotenv

from viktor_api_types import JobCreateResponse
from viktor_api_types import JobStatusResponse

load_dotenv()

logger = logging.getLogger(__name__)

API_BASE = os.getenv("VIKTOR_API_BASE", "https://beta.viktor.ai/api").rstrip("/")
VIKTOR_TOKEN = (os.getenv("TOKEN_VK_APP") or "").strip() or None
MAX_POLL_SECONDS = int(os.getenv("VIKTOR_MAX_POLL_SECONDS", "120"))
HTTP_CONNECT_TIMEOUT = float(os.getenv("VIKTOR_HTTP_CONNECT_TIMEOUT", "5"))
HTTP_READ_TIMEOUT = float(os.getenv("VIKTOR_HTTP_READ_TIMEOUT", "120"))


class ViktorTool(ABC):
    def __init__(
        self,
        workspace_id: int,
        entity_id: int,
        token: str | None = None,
        max_poll_seconds: int | None = None,
        api_base: str = API_BASE,
    ):
        self.workspace_id = workspace_id
        self.entity_id = entity_id
        self.token = (token or VIKTOR_TOKEN or "").strip()
        if not self.token:
            raise ValueError("Missing VIKTOR token (TOKEN_VK_APP).")

        self.max_poll_seconds = max_poll_seconds or MAX_POLL_SECONDS
        self.api_base = api_base.rstrip("/")
        self.job_url = f"{self.api_base}/workspaces/{self.workspace_id}/entities/{self.entity_id}/jobs/"

        self.auth_headers = {"Authorization": f"Bearer {self.token}"}
        self.json_headers = {**self.auth_headers, "Content-Type": "application/json"}

    @abstractmethod
    def build_payload(self) -> dict[str, Any]:
        raise NotImplementedError

    def run(self) -> JobStatusResponse:
        payload = self.build_payload()
        payload["poll_result"] = False

        logger.info("Submitting VIKTOR job to %s", self.job_url)
        logger.debug("Payload: %s", json.dumps(payload, indent=2))

        response = requests.post(
            url=self.job_url,
            headers=self.json_headers,
            json=payload,
            timeout=(HTTP_CONNECT_TIMEOUT, HTTP_READ_TIMEOUT),
        )

        if not response.ok:
            raise RuntimeError(
                f"Job submission failed (status={response.status_code}): {response.text[:500]}"
            )

        created_job = JobCreateResponse.model_validate(response.json())
        if created_job.url:
            return self.poll_job(created_job.url)

        if created_job.status == "success":
            return JobStatusResponse(
                uid=created_job.uid or 0,
                kind=created_job.kind or "result",
                status="success",
                result=created_job.content,
            )

        raise RuntimeError(f"Unexpected VIKTOR job response: {created_job.model_dump()}")

    def poll_job(self, job_url: str) -> JobStatusResponse:
        deadline = time.monotonic() + self.max_poll_seconds
        sleep_seconds = 0.8

        while time.monotonic() < deadline:
            response = requests.get(
                job_url,
                headers=self.auth_headers,
                timeout=(HTTP_CONNECT_TIMEOUT, HTTP_READ_TIMEOUT),
            )

            if not response.ok:
                raise RuntimeError(
                    f"Job polling failed (status={response.status_code}): {response.text[:500]}"
                )

            job = JobStatusResponse.model_validate(response.json())
            if job.is_success():
                return job

            if job.is_failed():
                error_message = job.get_error_message() or f"status={job.status}"
                raise RuntimeError(f"VIKTOR job failed: {error_message}")

            time.sleep(sleep_seconds)
            sleep_seconds = min(sleep_seconds * 1.5, 5.0)

        raise TimeoutError(f"Job did not finish within {self.max_poll_seconds} seconds.")

    def download_result_bytes(self, job: JobStatusResponse) -> bytes:
        if not job.download_url:
            raise ValueError("No download URL found in VIKTOR job result.")

        response = requests.get(
            job.download_url,
            timeout=(HTTP_CONNECT_TIMEOUT, HTTP_READ_TIMEOUT),
        )
        if not response.ok:
            raise RuntimeError(
                f"Failed to download result (status={response.status_code}): {response.text[:500]}"
            )

        return response.content
