using Dami.Data;
using Microsoft.EntityFrameworkCore;
using SoapCore;
using damisoap.service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=C:\\Users\\BOYRAZ\\Desktop\\damiservice\\dami.db"));

builder.Services.AddSoapCore();
builder.Services.AddScoped<IProductSoapService, ProductSoapService>();

var app = builder.Build();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.UseSoapEndpoint<IProductSoapService>(
        "/soap/products.asmx",
        new SoapEncoderOptions(),
        SoapSerializer.DataContractSerializer);
});

app.Run();