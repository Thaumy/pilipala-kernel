[<AutoOpen>]
module pilipala.builder.useAuth

open System.Net
open System.Net.Sockets
open fsharper.typ
open fsharper.alias
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open WebSocketer.typ
open pilipala.builder
open pilipala.service

//TODO：应使用随机化IV+CBC以代替ECB模式以获得最佳安全性

type Builder with

    member self.useAuth(port: u16) =

        let f _ =
            let server = //用于监听的服务器
                TcpListener(IPAddress.Parse("localhost"), i32 port)

            server.Start()

            Host
                .CreateDefaultBuilder()
                .ConfigureServices(fun services ->
                    services
                        //添加WS
                        .AddScoped<WebSocket>(fun _ ->
                            server.AcceptTcpClient()
                            |> fun c -> new WebSocket(c))
                        //添加服务主机
                        .AddHostedService<ServiceHost>()
                    |> ignore)
                .Build()
                .Run()

        { pipeline = self.pipeline .> effect f }
