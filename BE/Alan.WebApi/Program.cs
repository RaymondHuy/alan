using Alan.WebApi.Models;

namespace Alan.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            var configuration = builder.Configuration;

            var setting = new AzureDocumentSetting()
            {
                EndPoint = configuration.GetValue<string>("AzureDocumentEndPoint"),
                Key = configuration.GetValue<string>("AzureDocumentKey")
            };
            builder.Services.AddSingleton(setting);
            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.MapControllers();

            app.Run();
        }
    }
}