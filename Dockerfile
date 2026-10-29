# -------------------- 阶段1：编译发布 Build --------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. 复制整个仓库的所有文件（保证解决方案和各个类库的相对引用不丢失）
COPY . ./

# 2. 【请检查并修改】将这里的路径换成你真正的 Web 启动项目的 .csproj 相对路径
# 比如你的主程序在 src/SiteDeploy/SiteDeploy.csproj，就改成对应的路径
# 如果你的 csproj 文件直接在根目录，就写文件名即可
RUN dotnet restore "SiteDeploy.Document/SiteDeploy.Document.csproj"
RUN dotnet publish "SiteDeploy.Document/SiteDeploy.Document.csproj" -c Release -o /app/publish --no-self-contained

# -------------------- 阶段2：运行时 Runtime --------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 从编译阶段拷贝发布输出
COPY --from=build /app/publish ./

# Render 会自动注入 PORT 环境变量，程序监听这个端口
ENV ASPNETCORE_URLS=http://+:${PORT}

# 启动入口（确保这里的 dll 名称与你的主 Web 项目编译出来的 dll 一致）
ENTRYPOINT ["dotnet", "SiteDeploy.Document.dll"]