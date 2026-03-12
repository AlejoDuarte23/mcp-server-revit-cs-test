from datetime import datetime
from typing import Literal
from uuid import UUID

from pydantic import BaseModel
from pydantic import Field


class JobCreateRequest(BaseModel):
    """Payload for creating a VIKTOR job."""

    method_name: str = Field(..., min_length=1)
    params: dict | None = Field(default_factory=dict)
    poll_result: bool = Field(default=False)
    method_type: str | None = Field(default=None, min_length=1)
    editor_session: UUID | None = None
    events: list[str] = Field(default_factory=list)
    timeout: int = Field(default=86400, ge=1, le=86400)


JobStatus = Literal[
    "success",
    "cancelled",
    "failed",
    "running",
    "error",
    "error_user",
    "error_app_reloading",
    "error_timeout",
    "expired",
    "stopped",
    "message",
]


class JobErrorDetail(BaseModel):
    type: str | None = None
    message: str | None = None
    invalid_fields: dict | None = None


class JobMessage(BaseModel):
    message_type: Literal["progress"] | str | None = None
    message: str | None = None
    timestamp_epoch: int | None = None
    percentage: int | None = None


class DownloadResult(BaseModel):
    url: str


class JobResultPayload(BaseModel):
    model_config = {"extra": "allow"}

    download: DownloadResult | None = None

    @property
    def download_url(self) -> str | None:
        return self.download.url if self.download else None


class JobCreateResponse(BaseModel):
    uid: int | None = None
    url: str | None = None
    message: str | None = None
    kind: str | None = None
    status: JobStatus | None = None
    error_message: str | None = None
    error_stack_trace: dict | None = None
    invalid_fields: dict | None = None
    content: dict | None = None


class JobStatusResponse(BaseModel):
    uid: int
    kind: str
    status: JobStatus
    completed_at: datetime | None = None
    error: JobErrorDetail | None = None
    result: JobResultPayload | None = None
    message: JobMessage | None = None
    log_download_url: str | None = None

    def is_success(self) -> bool:
        return self.status == "success"

    def is_failed(self) -> bool:
        return self.status in {
            "failed",
            "cancelled",
            "error",
            "error_user",
            "error_timeout",
        }

    def get_error_message(self) -> str | None:
        if self.error and self.error.message:
            return self.error.message
        return None

    @property
    def download_url(self) -> str | None:
        return self.result.download_url if self.result else None
