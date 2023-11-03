using Alan.WebApi.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Alan.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddEnvironmentVariables();
                    config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true, true);
#if DEBUG
                    config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.{Environment.UserName}.json", true, true);
#endif
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }

    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var setting = new AzureDocumentSetting()
            {
                EndPoint = Configuration.GetValue<string>("AzureDocumentEndPoint"),
                Key = Configuration.GetValue<string>("AzureDocumentKey")
            };
            services.AddSingleton(setting);
            services.AddSingleton(new AzureOpenAISetting()
            {
                EndPoint = Configuration.GetValue<string>("AzureOpenAIEndPoint"),
                Key = Configuration.GetValue<string>("AzureOpenAIKey")
            });
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}