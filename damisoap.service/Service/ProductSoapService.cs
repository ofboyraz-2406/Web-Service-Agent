using Dami.Data;

namespace damisoap.service.Services;

public class ProductSoapService : IProductSoapService
{
    private readonly AppDbContext _db;
    public ProductSoapService(AppDbContext db) => _db = db;

    public List<Dami.Data.Models.Product> GetTop5Products()
    {
        return _db.Products
            .OrderBy(x => x.Id)
            .Take(5)
            .ToList();
    }
}