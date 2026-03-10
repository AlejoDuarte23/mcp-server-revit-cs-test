import asyncio
import os

import dotenv
from agents import Agent, Runner
from agents.mcp import MCPServerStreamableHttp

from table_tool import generate_table, show_hide_table_tool

dotenv.load_dotenv()

C_SHARP_MCP_SERVER_URL = os.getenv("CSHARP_MCP_SERVER_URL", "http://127.0.0.1:5099/mcp")
OPENAI_MODEL = os.getenv("OPENAI_MODEL", "gpt-5-mini")


async def agent(chat_history):
    instructions = (
        "You are a VIKTOR assistant connected to a C# Revit MCP server. "
        "Use the MCP tools exposed by the server to inspect the Revit session. "
        "You can also use the generate_table tool to display data in a structured table format. "
        "After calling generate_table, call show_hide_table with action='show' to display the Table view. "
        "Do not claim you can change the model unless there is a real tool for it. "
        "Prefer using discovered MCP tools instead of answering from memory."
    )

    async with MCPServerStreamableHttp(
        name="Revit MCP Server",
        params={
            "url": C_SHARP_MCP_SERVER_URL,
        },
        cache_tools_list=True,
        max_retry_attempts=3,
    ) as server:
        app_agent = Agent(
            name="Revit MCP Agent",
            instructions=instructions,
            model=OPENAI_MODEL,
            mcp_servers=[server],
            tools=[generate_table(), show_hide_table_tool()],
        )

        result = await Runner.run(app_agent, input=chat_history)
        return result.final_output


def agent_sync(chat_history):
    return asyncio.run(agent(chat_history))
