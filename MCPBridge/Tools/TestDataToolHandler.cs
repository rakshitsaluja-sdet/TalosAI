// MCPBridge/Tools/TestDataToolHandler.cs
using Bogus;
using McpBridge.Models;
using System.Text.Json;

namespace McpBridge.Tools;

public class TestDataToolHandler
{
    // Upper bound on generated-record count — an unbounded value here is a
    // trivial resource-exhaustion vector (e.g. count: 100000000).
    private const int MaxCount = 10_000;

    private static int GetBoundedCount(Dictionary<string, object> args, int defaultValue) =>
        Math.Clamp(
            args.ContainsKey("count") ? int.Parse(args["count"].ToString()!) : defaultValue,
            1, MaxCount);

    // ?? Generate Person Data ??????????????????????????????????????????
    public ToolResponse GeneratePersonData(Dictionary<string, object> args)
    {
        var count = GetBoundedCount(args, 1);
        var locale = args.GetValueOrDefault("locale", "en")?.ToString() ?? "en";

        var faker = new Faker(locale);
        var people = new List<object>();

        for (int i = 0; i < count; i++)
        {
            people.Add(new
            {
                firstName = faker.Name.FirstName(),
                lastName = faker.Name.LastName(),
                email = faker.Internet.Email(),
                phone = faker.Phone.PhoneNumber(),
                address = new
                {
                    street = faker.Address.StreetAddress(),
                    city = faker.Address.City(),
                    state = faker.Address.State(),
                    zipCode = faker.Address.ZipCode(),
                    country = faker.Address.Country()
                },
                dateOfBirth = faker.Date.Past(50, DateTime.Now.AddYears(-18)),
                company = faker.Company.CompanyName(),
                jobTitle = faker.Name.JobTitle()
            });
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            count = people.Count,
            locale,
            data = people
        });
    }

    // ?? Generate User Data ????????????????????????????????????????????
    public ToolResponse GenerateUserData(Dictionary<string, object> args)
    {
        var count = GetBoundedCount(args, 1);

        var faker = new Faker();
        var users = new List<object>();

        for (int i = 0; i < count; i++)
        {
            users.Add(new
            {
                username = faker.Internet.UserName(),
                password = faker.Internet.Password(12, false, "\\w", "@#$%"),
                email = faker.Internet.Email(),
                avatar = faker.Internet.Avatar(),
                userId = faker.Random.Guid(),
                registeredDate = faker.Date.Past(2)
            });
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            count = users.Count,
            data = users
        });
    }

    // ?? Generate Product Data ?????????????????????????????????????????
    public ToolResponse GenerateProductData(Dictionary<string, object> args)
    {
        var count = GetBoundedCount(args, 1);

        var faker = new Faker();
        var products = new List<object>();

        for (int i = 0; i < count; i++)
        {
            products.Add(new
            {
                productId = faker.Random.Guid(),
                productName = faker.Commerce.ProductName(),
                category = faker.Commerce.Categories(1)[0],
                price = decimal.Parse(faker.Commerce.Price()),
                description = faker.Commerce.ProductDescription(),
                sku = faker.Commerce.Ean13(),
                inStock = faker.Random.Bool(),
                stockQuantity = faker.Random.Number(0, 1000),
                imageUrl = faker.Image.PicsumUrl()
            });
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            count = products.Count,
            data = products
        });
    }

    // ?? Generate Order Data ???????????????????????????????????????????
    public ToolResponse GenerateOrderData(Dictionary<string, object> args)
    {
        var count = GetBoundedCount(args, 1);

        var faker = new Faker();
        var orders = new List<object>();

        for (int i = 0; i < count; i++)
        {
            var itemCount = faker.Random.Number(1, 5);
            var items = new List<object>();
            
            for (int j = 0; j < itemCount; j++)
            {
                items.Add(new
                {
                    productName = faker.Commerce.ProductName(),
                    quantity = faker.Random.Number(1, 10),
                    price = decimal.Parse(faker.Commerce.Price())
                });
            }

            orders.Add(new
            {
                orderId = faker.Random.Guid(),
                orderNumber = faker.Random.AlphaNumeric(10).ToUpper(),
                customerName = faker.Name.FullName(),
                orderDate = faker.Date.Past(1),
                status = faker.PickRandom(new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" }),
                items,
                totalAmount = items.Sum(item => 
                    decimal.Parse(((dynamic)item).price.ToString()) * 
                    int.Parse(((dynamic)item).quantity.ToString())
                ),
                shippingAddress = new
                {
                    street = faker.Address.StreetAddress(),
                    city = faker.Address.City(),
                    state = faker.Address.State(),
                    zipCode = faker.Address.ZipCode()
                }
            });
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            count = orders.Count,
            data = orders
        });
    }

    // ?? Generate Financial Data ???????????????????????????????????????
    public ToolResponse GenerateFinancialData(Dictionary<string, object> args)
    {
        var count = GetBoundedCount(args, 1);

        var faker = new Faker();
        var transactions = new List<object>();

        for (int i = 0; i < count; i++)
        {
            transactions.Add(new
            {
                transactionId = faker.Random.Guid(),
                accountNumber = faker.Finance.Account(),
                amount = faker.Finance.Amount(),
                currency = faker.Finance.Currency().Code,
                transactionType = faker.PickRandom(new[] { "Credit", "Debit", "Transfer" }),
                date = faker.Date.Past(1),
                description = faker.Lorem.Sentence(),
                status = faker.PickRandom(new[] { "Completed", "Pending", "Failed" }),
                creditCard = new
                {
                    number = faker.Finance.CreditCardNumber(),
                    cvv = faker.Finance.CreditCardCvv()
                }
            });
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            count = transactions.Count,
            data = transactions
        });
    }

    // ?? Generate Custom Data ??????????????????????????????????????????
    public ToolResponse GenerateCustomData(Dictionary<string, object> args)
    {
        var count = GetBoundedCount(args, 1);
        var dataType = args["data_type"].ToString()!;
        
        var faker = new Faker();
        var data = new List<object>();

        for (int i = 0; i < count; i++)
        {
            object item = dataType.ToLower() switch
            {
                "lorem" => new { text = faker.Lorem.Paragraph(), words = faker.Lorem.Words(), sentence = faker.Lorem.Sentence() },
                "internet" => new { email = faker.Internet.Email(), url = faker.Internet.Url(), ipv4 = faker.Internet.Ip(), ipv6 = faker.Internet.Ipv6() },
                "date" => new { past = faker.Date.Past(), future = faker.Date.Future(), recent = faker.Date.Recent(), soon = faker.Date.Soon() },
                "vehicle" => new { manufacturer = faker.Vehicle.Manufacturer(), model = faker.Vehicle.Model(), vin = faker.Vehicle.Vin(), fuel = faker.Vehicle.Fuel() },
                "system" => new { fileName = faker.System.FileName(), extension = faker.System.FileExt(), filePath = faker.System.FilePath(), mimeType = faker.System.MimeType() },
                _ => (object)new { random = faker.Random.AlphaNumeric(20) }
            };
            
            data.Add(item);
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            dataType,
            count = data.Count,
            data
        });
    }

    // ?? Generate Batch Test Data ??????????????????????????????????????
    public ToolResponse GenerateBatchTestData(Dictionary<string, object> args)
    {
        var count = GetBoundedCount(args, 10);

        return ToolResponse.Ok(new
        {
            status = "ok",
            users = GenerateUserData(new Dictionary<string, object> { { "count", count } }).Result,
            products = GenerateProductData(new Dictionary<string, object> { { "count", count } }).Result,
            orders = GenerateOrderData(new Dictionary<string, object> { { "count", count } }).Result
        });
    }
}
