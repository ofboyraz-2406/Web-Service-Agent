using System;
using System.Collections.Generic;
using System.Web.Services;
using System.Data.SQLite;

namespace damisoap_1_
{
    [WebService(Namespace = "http://tempuri.org/")]
    public class ProductsService : WebService
    {
        private const string DbPath = @"C:\Users\BOYRAZ\Desktop\damiservice\dami.db";
        private static readonly string ConnStr = $@"Data Source={DbPath};Version=3;";

        [WebMethod]
        public List<ProductDto> GetTop5Products()
        {
            
            var def = GetDefinition(1);
            if (def.IsActive != 1)
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    LogOperation(conn, 1, "", "Definition pasif olduğu için çağrı yapılmadı", "FAILED");
                }
                return new List<ProductDto>();
            }

            var products = new List<ProductDto>();

            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();

                const string sql = @"SELECT Id, Name, Price
                                     FROM PRODUCTS
                                     ORDER BY Id DESC
                                     LIMIT 5;";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(new ProductDto
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Name = reader["Name"].ToString(),
                            Price = Convert.ToDecimal(reader["Price"])
                        });
                    }
                }

                LogOperation(conn, 1, "", $"Returned {products.Count} products", "SUCCESS");
            }

            return products;
        }

        private void LogOperation(SQLiteConnection conn, int definitionId, string request, string response, string status)
        {
            const string insertSql = @"
INSERT INTO OPERATIONS (DefinitionId, RequestPayload, ResponsePayload, Status, CreatedAt)
VALUES (@defId, @req, @res, @st, @dt);";

            using (var cmd = new SQLiteCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@defId", definitionId);
                cmd.Parameters.AddWithValue("@req", request);
                cmd.Parameters.AddWithValue("@res", response);
                cmd.Parameters.AddWithValue("@st", status);
                cmd.Parameters.AddWithValue("@dt", DateTime.Now);
                //cmd.ExecuteNonQuery();
            }
        }

        private class DefinitionRow
        {
            public int Id { get; set; }
            public string ServiceUrl { get; set; } = "";
            public string MethodName { get; set; } = "";
            public string ServiceType { get; set; } = "";
            public int IsActive { get; set; }
        }

        private DefinitionRow GetDefinition(int definitionId)
        {
            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();

                const string sql = @"
SELECT Id, ServiceUrl, MethodName, ServiceType, IsActive
FROM DEFINITIONS
WHERE Id = @id
LIMIT 1;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", definitionId);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            throw new Exception("Definition bulunamadı: " + definitionId);

                        return new DefinitionRow
                        {
                            Id = Convert.ToInt32(r["Id"]),
                            ServiceUrl = r["ServiceUrl"].ToString(),
                            MethodName = r["MethodName"].ToString(),
                            ServiceType = r["ServiceType"].ToString(),
                            IsActive = Convert.ToInt32(r["IsActive"])
                        };
                    }
                }
            }
        }
    }

    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }
}