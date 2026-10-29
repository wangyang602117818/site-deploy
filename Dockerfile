# -------------------- 阶段1：编译发布 Build --------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 先复制csproj，利用docker缓存，加速构建
COPY *.csproj ./
RUN dotnet restore

# 复制全部源代码
COPY . ./

# Release编译发布，输出到 /app/publish
RUN dotnet publish -c Release -o /app/publish --no-self-contained

# -------------------- 阶段2：运行时 Runtime --------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 从编译阶段拷贝发布输出
COPY --from=build /app/publish ./

# Render会自动注入PORT环境变量，程序监听这个端口
ENV ASPNETCORE_URLS=http://+:${PORT}

# 启动入口
ENTRYPOINT ["dotnet", "SiteDeploy.Document.dll"]
