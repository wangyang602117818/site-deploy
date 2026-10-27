# 🚀 IIS 站点发布管理系统 (SiteDeploy)

<p align="center">
  <b>简体中文</b> | <a href="./README.md">English</a>
</p>

> **一句话简介**：支持 IIS 站点自动化发布、多服务器集群统一部署、一键自动备份与版本还原的高效运维工具。

---

## 📖 目录导航

- [✨ 功能特性](#-功能特性)
- [🛠️ 安装与配置](#️-安装与配置)
  - [单服务器部署](#1-单服务器部署iis-模式)
  - [多服务器部署](#2-多服务器部署iis-模式)
  - [反向代理配置](#3-反向代理配置高安全性环境)
- [💡 使用指南](#-使用指南)
  - [系统登录](#系统登录)
  - [权限管理](#权限管理)
  - [使用案例](#使用案例)

---

## ✨ 功能特性

- **🌍 多语言支持**：原生支持简体中文与英文切换，无缝适应不同团队。
- **📦 核心发布**：支持一键打包、上传、覆盖并重启 IIS 站点。
- **🌐 多机协同**：支持单机或多服务器集群统一管理与分发部署。
- **🛡️ 安全保障**：内置自动化备份与快速版本还原机制，发布失败秒级回滚。

![发布演示](./readme-images/pub_1.gif)

---

## 🛠️ 安装与配置

### 1. 单服务器部署（IIS 模式）

1. **下载与解压**：下载主节点安装包 `SiteDeploy.zip`，并解压至 `C:\inetpub\wwwroot\SiteDeploy`。
2. **配置应用程序池**：
   - 名称：`sitedeploy`
   - .NET CLR 版本：`无托管代码`
   - 托管管道模式：`集成`
   - 高级设置：将 **标识 (Identity)** 由 `ApplicationPoolIdentity` 更改为 **`LocalSystem`**。
3. **添加网站**：
   - 网站名称：`SiteDeploy`
   - 应用程序池：选择 `sitedeploy`
   - 物理路径：`C:\inetpub\wwwroot\SiteDeploy`
   - 端口号：自定义（例如 `8080`）
4. **访问与初始化**：
   - 访问地址：`http://localhost:8080`
   - **登录凭证**：
     - 用户名：`admin`
     - 初始密码：查看站点根目录下的 `admin_initial_password.txt` 文件。
5. **配置文件说明 (`appsettings.json`)**：
   - `SystemConfig.Urls`：配置从节点的 URL 地址。
   - `SystemConfig.IpWhitelist`：IP 白名单，用于限制访问。
   - `BindUrl`：当前站点绑定的 URL 地址（若配置为 Windows 服务时需填写，IIS 站点可忽略）。
   - `BasePath`：站点的基础路径。

---

### 2. 多服务器部署（IIS 模式）

> **前提条件**：请确保主节点已成功部署并运行。

1. **下载与解压**：下载从节点安装包 `SiteDeploy.Node.zip`，并解压至 `C:\inetpub\wwwroot\SiteDeploy.Node`。
2. **配置应用程序池**：
   - 名称：`sitedeploy.node`
   - .NET CLR 版本：`无托管代码`
   - 托管管道模式：`集成`
   - 高级设置：将 **标识 (Identity)** 由 `ApplicationPoolIdentity` 更改为 **`LocalSystem`**。
3. **添加网站**：
   - 网站名称：`SiteDeploy.Node`
   - 应用程序池：选择 `sitedeploy.node`
   - 物理路径：`C:\inetpub\wwwroot\SiteDeploy.Node`
   - 端口号：自定义（例如 `8081`）
4. **验证从节点**：
   - 访问 `http://localhost:8081`。若页面正常显示当前站点的 URL，则说明配置成功（从节点无需登录）。
   - **配置文件说明 (`appsettings.json`)**：
     - `SystemConfig.IpWhitelist`：IP 白名单限制。
     - `BindUrl`：Windows 服务模式下的绑定地址（IIS 模式可忽略）。
     - `BasePath`：站点基础路径。
5. **关联主节点**：
   - 在主节点的配置文件 (`appsettings.json`) 中，将从节点 URL 添加至 `SystemConfig.Urls`（例如：`http://localhost:8081`）。

---

### 3. 反向代理配置（高安全性环境）

> 💡 **适用场景**：在安全性要求较高的环境中，若不便将应用程序池标识切换为 `LocalSystem`，可通过反向代理方式运行。

1. **修改配置文件 (`appsettings.json`)**：
   - 设置反向代理地址 `BindUrl`（例如：`http://127.0.0.1:5002`）。
   - 设置站点基础路径 `BasePath`（例如：`sitedeploy`）。
2. **安装并启动 Windows 服务**：
   - 以管理员身份运行根目录下的 `install-service.bat` 脚本。
   - 确保浏览器可以正常访问 `http://127.0.0.1:5002/sitedeploy`。
3. **配置 IIS 反向代理**：
   - 确保 IIS 已安装反向代理模块（**URL Rewrite** + **Application Request Routing (ARR)**）。
   - 创建空目录：`C:\inetpub\wwwroot\SiteDeploy-Proxy`。
   - 在 IIS 中新增网站，绑定端口（如 `8080`），物理路径指向上述创建的代理目录。
   - 替换该站点的 `web.config` 文件内容：
     ```xml
     <?xml version="1.0" encoding="UTF-8"?>
     <configuration>
       <system.webServer>
         <rewrite>
           <rules>
             <rule name="ReverseProxyInboundRule1" stopProcessing="true">
               <match url="(.*)" />
               <action type="Rewrite" url="[http://127.0.0.1:5002/sitedeploy/](http://127.0.0.1:5002/sitedeploy/){R:1}" />
             </rule>
           </rules>
         </rewrite>
       </system.webServer>
     </configuration>
     ```
4. **访问系统**：浏览器访问 `http://localhost:8080` 即可使用。

---

## 💡 使用指南

### 系统登录
> ⚠️ **注意**：统一在主节点进行登录，从节点不提供登录界面。
- **默认账号**：`admin`
- **默认密码**：站点根目录下的 `admin_initial_password.txt` 文件中。

### 权限管理
- 管理员登录后可创建子用户，并支持精细化权限控制，指定用户可操作的服务器及具体站点。
- **配置示例**：允许 `user1` 操作 `http://localhost:5000`（crm-01, crm-02 站点）以及 `http://localhost:5001`（app01, crm-01, crm-02 站点）。

![权限管理配置](./readme-images/per_1.gif)

### 使用案例

- **单站点发布**：
  ![单站点发布](./readme-images/pub_2.gif)

- **多站点协同发布**：
  ![多站点发布](./readme-images/pub_3.gif)

- **版本快速回滚/还原**（以 `http://localhost:5000` 和 `http://localhost:5001` 为例）：
  ![版本还原](./readme-images/pub_4.gif)