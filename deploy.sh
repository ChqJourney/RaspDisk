#!/bin/bash

# 设置错误时退出
set -e

# 读取配置文件
config_file="deploy.config.json"
if [ ! -f "$config_file" ]; then
    echo "错误: 找不到配置文件 $config_file"
    exit 1
fi

# 解析配置文件
host=$(jq -r '.raspberryPi.host' "$config_file")
username=$(jq -r '.raspberryPi.username' "$config_file")
deploy_path=$(jq -r '.raspberryPi.deployPath' "$config_file")
service_name=$(jq -r '.server.serviceName' "$config_file")
port=$(jq -r '.server.port' "$config_file")

echo "开始部署流程..."

# 1. 构建Svelte前端项目
echo "正在构建前端项目..."
cd web
pnpm install
pnpm run build
cd ..

# 清理并复制前端文件到wwwroot
#rm -rf RaspberryPiFileServer/wwwroot/*
#cp -r web/build/* RaspberryPiFileServer/wwwroot/

# 2. 构建.NET项目
echo "正在构建后端项目..."
cd RaspberryPiFileServer
rm -rf ./publish  # 清理之前的发布目录
dotnet clean      # 清理项目
dotnet publish -c Release -r linux-arm64 --self-contained true -o ./publish
cd ..

# 3. 部署到树莓派
echo "正在部署到树莓派..."

# 创建部署目录并设置权限
ssh -o StrictHostKeyChecking=no "$username@$host" "
    if [ ! -d \"$deploy_path\" ]; then
        sudo mkdir -p \"$deploy_path\"
        echo \"创建部署目录: $deploy_path\"
    fi
    sudo chown -R $username:$username \"$deploy_path\"
    sudo chmod -R 755 \"$deploy_path\""

# 停止现有服务
echo "正在停止服务..."
ssh "$username@$host" "sudo systemctl stop $service_name || true"

# 复制文件到树莓派
echo "正在复制文件..."
scp -r RaspberryPiFileServer/publish/* "$username@$host:$deploy_path"

# 重启服务
echo "正在启动服务..."
ssh "$username@$host" "
    sudo systemctl daemon-reload && \
    sudo systemctl enable $service_name && \
    sudo systemctl start $service_name && \
    sudo systemctl restart nginx"


# 创建systemd服务文件（如果不存在）
service_file=$(cat << EOF
[Unit]
Description=File Server Service
After=network.target

[Service]
WorkingDirectory=$deploy_path
ExecStart=$deploy_path/RaspberryPiFileServer
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$service_name
User=$username
Environment=ASPNETCORE_URLS="http://0.0.0.0:$port"

[Install]
WantedBy=multi-user.target
EOF
)

ssh "$username@$host" "
    if [ ! -f /etc/systemd/system/$service_name.service ]; then
        echo \"$service_file\" | sudo tee /etc/systemd/system/$service_name.service > /dev/null
        echo '创建新的服务文件'
    fi"

# 重启服务
ssh "$username@$host" "
    sudo systemctl daemon-reload && \
    sudo systemctl enable $service_name && \
    sudo systemctl start $service_name && \
    sudo systemctl restart nginx"

# 5. 验证服务是否正常运行
echo "正在验证服务..."
sleep 5

# 检查服务状态
service_status=$(ssh "$username@$host" "systemctl is-active $service_name")
if [ "$service_status" = "active" ]; then
    echo "服务已成功启动！"
    echo "请访问 http://$host:$port 验证服务是否正常运行"
else
    echo "错误: 服务启动失败"
    exit 1
fi