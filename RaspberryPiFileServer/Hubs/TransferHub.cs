using Microsoft.AspNetCore.SignalR;

namespace RaspberryPiFileServer.Hubs;

public class TransferHub : Hub
{
    private readonly ILogger<TransferHub> _logger;

    public TransferHub(ILogger<TransferHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("客户端连接成功: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("客户端断开连接: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendTransferProgress(string fileName, int progress)
    {
        await Clients.All.SendAsync("ReceiveTransferProgress", fileName, progress);
    }

    public async Task SendTransferComplete(string fileName)
    {
        await Clients.All.SendAsync("ReceiveTransferComplete", fileName);
    }

    public async Task SendTransferError(string fileName, string error)
    {
        await Clients.All.SendAsync("ReceiveTransferError", fileName, error);
    }
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}