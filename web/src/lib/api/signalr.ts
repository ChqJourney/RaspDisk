import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { apiConfig } from './config';

export class SignalRService {
    private connection: HubConnection;
    private reconnectDelay = 0;
    private maxReconnectDelay = 30000; // 最大重连延迟30秒
    private readonly baseDelay = 1000; // 基础重连延迟1秒

    constructor() {
        this.connection = new HubConnectionBuilder()
            .withUrl(`${apiConfig.baseURL.replace('/api', '')}/transferHub`, {
                withCredentials: false,
                transport: 1, // 使用WebSockets传输
                accessTokenFactory: () => localStorage.getItem('apiKey') || '',
                headers: {
                    'X-api-Key': localStorage.getItem('apiKey') || ''
                }
            })
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Information)
            .build();

        this.setupConnectionEvents();
    }

    private setupConnectionEvents() {
        this.connection.onreconnecting(() => {
            console.log('正在重新连接到服务器...');
        });

        this.connection.onreconnected(() => {
            console.log('已重新连接到服务器');
            this.reconnectDelay = 0;
        });

        this.connection.onclose(() => {
            console.log('连接已关闭');
            this.reconnectWithBackoff();
        });
    }

    private async reconnectWithBackoff() {
        try {
            await new Promise(resolve => setTimeout(resolve, this.reconnectDelay));
            await this.start();
            this.reconnectDelay = 0;
        } catch {
            this.reconnectDelay = Math.min(
                (this.reconnectDelay || this.baseDelay) * 2,
                this.maxReconnectDelay
            );
            this.reconnectWithBackoff();
        }
    }

    public async start() {
        try {
            await this.connection.start();
            console.log('已连接到SignalR服务器');
        } catch (err) {
            console.error('连接到SignalR服务器失败:', err);
            throw err;
        }
    }

    public onTransferProgress(callback: (fileName: string, progress: number) => void) {
        this.connection.on('ReceiveTransferProgress', callback);
    }

    public onTransferComplete(callback: (fileName: string) => void) {
        this.connection.on('ReceiveTransferComplete', callback);
    }

    public onTransferError(callback: (fileName: string, error: string) => void) {
        this.connection.on('ReceiveTransferError', callback);
    }

    public async stop() {
        try {
            await this.connection.stop();
            console.log('已断开与SignalR服务器的连接');
        } catch (err) {
            console.error('断开连接时发生错误:', err);
        }
    }
}

