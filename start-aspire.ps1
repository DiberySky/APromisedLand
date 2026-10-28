# APromisedLand Aspire 编排启动脚本
# 用法：powershell -File .\start-aspire.ps1
# 前置：Docker Desktop 已启动（vllm / vllm-embed / NebulaGraph / Redis / Postgres 等容器依赖）

$ErrorActionPreference = 'Stop'
$appHost = Join-Path $PSScriptRoot 'APromisedLand.AppHost'

# 1. 检查 Docker 是否可用
Write-Host '[1/3] 检查 Docker...' -ForegroundColor Cyan
try {
    docker info --format '{{.ServerVersion}}' | Out-Null
} catch {
    Write-Error 'Docker 不可用。请先启动 Docker Desktop 后重试。'
    exit 1
}

# 2. 构建 AppHost（快速失败：编译错误直接退出）
Write-Host '[2/3] 构建 AppHost...' -ForegroundColor Cyan
dotnet build $appHost -v q
if ($LASTEXITCODE -ne 0) {
    Write-Error "AppHost 构建失败 (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

# 3. 启动 Aspire 编排（Dashboard 地址见控制台输出）
Write-Host '[3/3] 启动 Aspire AppHost（Ctrl+C 停止）...' -ForegroundColor Cyan
dotnet run --project $appHost --no-build
