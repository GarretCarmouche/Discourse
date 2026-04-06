using Microsoft.AspNetCore.Components;

namespace BlazorApp2.Components.Layout;

public partial class ServerBrowser : ComponentBase
{
    public List<string> GetServers()
    {
        return new List<string> {"Default"};
    }
}