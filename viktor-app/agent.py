import asyncio
import os
import queue
import threading
from collections.abc import Callable
from collections.abc import Iterator
from typing import Any

import dotenv
from agents import Agent
from agents import Runner
from agents import set_tracing_disabled
from agents.mcp import MCPServerStreamableHttp
from openai.types.responses import ResponseTextDeltaEvent

from table_tool import generate_table
from table_tool import show_hide_table_tool

dotenv.load_dotenv()
set_tracing_disabled(True)

C_SHARP_MCP_SERVER_URL = os.getenv("CSHARP_MCP_SERVER_URL", "http://127.0.0.1:5099/mcp")
OPENAI_MODEL = os.getenv("OPENAI_MODEL", "gpt-5-mini")

TOOL_DISPLAY_NAMES = {
    "generate_table": "Generate Table",
    "show_hide_table": "Show/Hide Table",
}

_event_loop: asyncio.AbstractEventLoop | None = None
_event_loop_thread: threading.Thread | None = None


def ensure_loop() -> asyncio.AbstractEventLoop:
    global _event_loop, _event_loop_thread
    if _event_loop and _event_loop.is_running():
        return _event_loop

    _event_loop = asyncio.new_event_loop()
    _event_loop_thread = threading.Thread(
        target=_event_loop.run_forever,
        name="viktor-agent-stream-loop",
        daemon=True,
    )
    _event_loop_thread.start()
    return _event_loop


def _extract_call_id(raw: Any) -> str | None:
    if isinstance(raw, dict):
        for key in ("call_id", "id", "tool_call_id"):
            value = raw.get(key)
            if value:
                return str(value)
        return None

    for attr in ("call_id", "id", "tool_call_id"):
        value = getattr(raw, attr, None)
        if value:
            return str(value)
    return None


def _extract_tool_name(raw: Any) -> str:
    if isinstance(raw, dict):
        if raw.get("name"):
            return str(raw["name"])
        if raw.get("tool_name"):
            return str(raw["tool_name"])
        function = raw.get("function")
        if isinstance(function, dict) and function.get("name"):
            return str(function["name"])

    for attr in ("name", "tool_name", "function_name"):
        value = getattr(raw, attr, None)
        if value:
            return str(value)

    function = getattr(raw, "function", None)
    if function is not None and getattr(function, "name", None):
        return str(function.name)
    return "tool"


def _display_tool_name(raw_tool_name: str) -> str:
    return TOOL_DISPLAY_NAMES.get(raw_tool_name, raw_tool_name)


def _build_agent(server: MCPServerStreamableHttp) -> Agent:
    instructions = (
        "You are a VIKTOR assistant connected to a C# Revit MCP server. "
        "Use MCP tools when possible instead of answering from memory. "
        "Markdown is allowed, but do not use markdown tables. "
        "If you call generate_table, also call show_hide_table with action='show'. "
        "Do not claim write capabilities unless a real tool is available."
    )

    return Agent(
        name="Revit MCP Agent",
        instructions=instructions,
        model=OPENAI_MODEL,
        mcp_servers=[server],
        tools=[generate_table(), show_hide_table_tool()],
    )


def agent_sync_stream(
    chat_history: list[dict[str, str]],
    *,
    on_done: Callable[[], None] | None = None,
    show_tool_progress: bool = True,
) -> Iterator[str]:
    output_queue: queue.Queue[object] = queue.Queue()
    sentinel = object()
    loop = ensure_loop()

    async def _produce() -> None:
        call_id_to_name: dict[str, str] = {}

        try:
            async with MCPServerStreamableHttp(
                name="Revit MCP Server",
                params={"url": C_SHARP_MCP_SERVER_URL},
                cache_tools_list=True,
                max_retry_attempts=3,
            ) as server:
                app_agent = _build_agent(server)
                streamed_result = Runner.run_streamed(app_agent, input=chat_history, max_turns=20)  # type: ignore[arg-type]

                async for event in streamed_result.stream_events():
                    if event.type == "raw_response_event" and isinstance(
                        event.data, ResponseTextDeltaEvent
                    ):
                        if event.data.delta:
                            output_queue.put(event.data.delta)
                        continue

                    if not show_tool_progress:
                        continue

                    if event.type == "run_item_stream_event":
                        raw = getattr(event.item, "raw_item", None)

                        if event.name == "tool_called":
                            call_id = _extract_call_id(raw)
                            tool_name = _extract_tool_name(raw)
                            if call_id:
                                call_id_to_name[call_id] = tool_name
                            display_name = _display_tool_name(tool_name)
                            output_queue.put(f"\n\n**Tool running:** `{display_name}`\n\n")
                            continue

                        if event.name == "tool_output":
                            call_id = _extract_call_id(raw)
                            tool_name = call_id_to_name.get(call_id or "", "tool")
                            display_name = _display_tool_name(tool_name)
                            output_queue.put(f"\n\n**Tool done:** `{display_name}`\n\n")
                            continue

        except Exception as error:
            output_queue.put(f"\n\n⚠️ `{type(error).__name__}: {error}`\n\n")
        finally:
            output_queue.put(sentinel)

    asyncio.run_coroutine_threadsafe(_produce(), loop)

    def _stream() -> Iterator[str]:
        while True:
            item = output_queue.get()
            if item is sentinel:
                break
            yield str(item)
        if on_done:
            on_done()

    return _stream()


async def agent(chat_history: list[dict[str, str]]) -> str:
    async with MCPServerStreamableHttp(
        name="Revit MCP Server",
        params={"url": C_SHARP_MCP_SERVER_URL},
        cache_tools_list=True,
        max_retry_attempts=3,
    ) as server:
        app_agent = _build_agent(server)
        result = await Runner.run(app_agent, input=chat_history)
        return str(result.final_output or "")


def agent_sync(chat_history: list[dict[str, str]]) -> str:
    return asyncio.run(agent(chat_history))
