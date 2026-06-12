using Dami.Data.Models;
using System.ServiceModel;

namespace damisoap.service.Services;

[ServiceContract]
public interface IProductSoapService
{
    [OperationContract]
    List<Product> GetTop5Products();
}