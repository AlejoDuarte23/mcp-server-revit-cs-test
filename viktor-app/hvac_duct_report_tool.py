import asyncio
import json
import os
from typing import Literal
from typing import Any

import viktor as vkt
from pydantic import BaseModel
from pydantic import Field
from pydantic import model_validator

from viktor_tool_base import ViktorTool


class DuctElementRow(BaseModel):
    rank: int = Field(description="Rank by pressure drop.")
    element_id: int = Field(description="Revit element id.")
    unique_id: str = Field(description="Revit unique id.")
    element_name: str = Field(description="Element name or type.")
    pressure_drop: float = Field(description="Pressure drop in Pa.")
    flow: float = Field(description="Flow in L/s.")
    size: str = Field(description="Duct size text.")
    length: float = Field(description="Element length in mm.")
    velocity: float = Field(description="Velocity in m/s.")
    friction: float = Field(description="Friction in Pa/m.")
    loss_coefficient: float = Field(description="Loss coefficient.")
    system_name: str = Field(description="MEP system name.")
    reference_level: str = Field(description="Reference level.")
    mark: int = Field(description="Element mark.")


def _default_rows() -> list[DuctElementRow]:
    return [
        DuctElementRow(
            rank=1,
            element_id=520209,
            unique_id="f056ea9d-8fba-434e-80ce-46b4df0ea740-0007f011",
            element_name="Mitered Elbows / Taps (Rectangular Duct)",
            pressure_drop=27.2,
            flow=755.1,
            size="450 mm x 300 mm",
            length=30873.0,
            velocity=5.6,
            friction=0.88,
            loss_coefficient=1.443216,
            system_name="Mechanical Exhaust Air 1",
            reference_level="Level 1",
            mark=1232,
        ),
        DuctElementRow(
            rank=2,
            element_id=522128,
            unique_id="9d64d9f8-5eaf-449e-98dc-7a24fdfd39af-0007f790",
            element_name="Mitered Elbows / Taps (Rectangular Duct)",
            pressure_drop=13.6,
            flow=500.0,
            size="450 mm x 300 mm",
            length=32520.0,
            velocity=3.7,
            friction=0.42,
            loss_coefficient=1.652763,
            system_name="Mechanical Exhaust Air 3",
            reference_level="Level 3",
            mark=1278,
        ),
        DuctElementRow(
            rank=3,
            element_id=517428,
            unique_id="810e919f-3214-4598-bcad-93bb149143c0-0007e534",
            element_name="Mitered Elbows / Taps (Rectangular Duct)",
            pressure_drop=9.3,
            flow=755.1,
            size="450 mm x 275 mm",
            length=8499.0,
            velocity=6.1,
            friction=1.1,
            loss_coefficient=0.416022,
            system_name="Mechanical Exhaust Air 1",
            reference_level="Level 1",
            mark=1210,
        ),
    ]


class HvacDuctReportInput(BaseModel):
    workspace_id: int | None = Field(
        default=5108,
        description="VIKTOR workspace id. Defaults to 5108.",
    )
    entity_id: int | None = Field(
        default=2869,
        description="VIKTOR entity id. Defaults to 2869.",
    )
    elements: list[DuctElementRow] = Field(
        default_factory=_default_rows,
        description="Table rows for input_section.elements in the VIKTOR app.",
    )
    output_mode: Literal["link", "store_pdf"] = Field(
        default="link",
        description=(
            "Result mode: 'link' returns the VIKTOR/S3 download URL only; "
            "'store_pdf' downloads the PDF bytes and stores them in VIKTOR Storage."
        ),
    )

    @model_validator(mode="after")
    def ensure_rows(self) -> "HvacDuctReportInput":
        if not self.elements:
            raise ValueError("elements must contain at least one row.")
        return self


class HvacDuctReportTool(ViktorTool):
    def __init__(
        self,
        report_input: HvacDuctReportInput,
        workspace_id: int,
        entity_id: int,
        method_name: str = "download_report",
    ):
        super().__init__(workspace_id=workspace_id, entity_id=entity_id)
        self.report_input = report_input
        self.method_name = method_name

    def build_payload(self) -> dict[str, Any]:
        return {
            "method_name": self.method_name,
            "params": {
                "input_section": {
                    "elements": [row.model_dump() for row in self.report_input.elements]
                }
            },
            "poll_result": True,
        }

    def run_and_download_pdf(self) -> bytes:
        job = self.run()
        return self.download_result_bytes(job)

    def run_and_get_download_url(self) -> str:
        job = self.run()
        if not job.download_url:
            raise ValueError("No download URL found in VIKTOR job result.")
        return job.download_url


def _resolve_target_ids(input_data: HvacDuctReportInput) -> tuple[int, int]:
    workspace_id = input_data.workspace_id or int(
        os.getenv("VIKTOR_HVAC_REPORT_WORKSPACE_ID", "5108")
    )
    entity_id = input_data.entity_id or int(
        os.getenv("VIKTOR_HVAC_REPORT_ENTITY_ID", "2869")
    )

    if workspace_id <= 0 or entity_id <= 0:
        raise ValueError(
            "Missing target app ids. Provide workspace_id/entity_id or set "
            "VIKTOR_HVAC_REPORT_WORKSPACE_ID and VIKTOR_HVAC_REPORT_ENTITY_ID."
        )
    return workspace_id, entity_id


async def generate_hvac_duct_pdf_report_func(ctx: Any, args: str) -> str:
    input_data = HvacDuctReportInput.model_validate_json(args)
    workspace_id, entity_id = _resolve_target_ids(input_data)

    tool = HvacDuctReportTool(
        report_input=input_data,
        workspace_id=workspace_id,
        entity_id=entity_id,
        method_name="download_report",
    )

    payload_preview = tool.build_payload()
    if input_data.output_mode == "link":
        download_url = await asyncio.to_thread(tool.run_and_get_download_url)
        return (
            "HVAC duct PDF job completed.\n"
            f"- method_name: `{payload_preview['method_name']}`\n"
            f"- workspace_id: `{workspace_id}`\n"
            f"- entity_id: `{entity_id}`\n"
            f"- rows: `{len(input_data.elements)}`\n"
            f"- download_url: `{download_url}`\n"
            f"- payload_preview: ```json\n{json.dumps(payload_preview, indent=2)[:4000]}\n```"
        )

    pdf_bytes = await asyncio.to_thread(tool.run_and_download_pdf)
    vkt.Storage().set(
        "HvacDuctPdfReport",
        data=vkt.File.from_data(pdf_bytes),
        scope="entity",
    )

    return (
        "HVAC duct PDF generated and stored.\n"
        f"- method_name: `{payload_preview['method_name']}`\n"
        f"- workspace_id: `{workspace_id}`\n"
        f"- entity_id: `{entity_id}`\n"
        f"- rows: `{len(input_data.elements)}`\n"
        f"- pdf_bytes: `{len(pdf_bytes)}`\n"
        f"- payload_preview: ```json\n{json.dumps(payload_preview, indent=2)[:4000]}\n```"
    )


def generate_hvac_duct_pdf_report_tool() -> Any:
    from agents import FunctionTool

    return FunctionTool(
        name="generate_hvac_duct_pdf_report",
        description=(
            "Generate and download the HVAC duct pressure-drop PDF report by calling a VIKTOR app "
            "DownloadButton method. This tool sends params.input_section.elements and executes "
            "method_name='download_report'."
        ),
        params_json_schema=HvacDuctReportInput.model_json_schema(),
        on_invoke_tool=generate_hvac_duct_pdf_report_func,
    )
