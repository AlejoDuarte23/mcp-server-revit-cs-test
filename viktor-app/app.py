import viktor as vkt

from agent import agent_sync


class Parametrization(vkt.Parametrization):
    intro = vkt.Text(
        """
        ## Revit MCP Agent

        This Viktor app talks to the local C# Revit MCP endpoint at `CSHARP_MCP_SERVER_URL`
        and discovers tools through the MCP protocol using OpenAI Agents Python.
        """
    )
    chat = vkt.Chat(
        "Ask the Revit bridge",
        method="call_llm",
        visible=True,
        flex=100,
    )


class Controller(vkt.Controller):
    label = "Revit MCP Agent"
    parametrization = Parametrization

    def call_llm(self, params, **kwargs):
        return agent_sync(params.chat)
