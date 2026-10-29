# -------------------- 阶段1：编译发布 Build --------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. 复制整个解决方案或所有源码与项目文件（因为项目和解法方案在一起，直接复制全部或按需复制）
COPY . ./

# 2. 针对你的【主启动项目】进行还原（请将 SiteDeploy.Document.csproj 替换为你真正的 Web 站点项目文件名）
RUN dotnet restore "SiteDeploy.Document.csproj"

# 3. 指定只发布主启动项目，输出到 /app/publish
RUN dotnet publish "SiteDeploy.Document.csproj" -c Release -o /app/publish --no-self-contained

# -------------------- 阶段2：运行时 Runtime --------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 从编译阶段拷贝发布输出
COPY --from=build /app/publish ./

# Render会自动注入PORT环境变量，程序监听这个端口
ENV ASPNETCORE_URLS=http://+:${PORT}

# 启动入口（确保这里的 dll 名称是你的主 Web 项目生成的 dll）
ENTRYPOINT ["dotnet", "SiteDeploy.Document.dll"]