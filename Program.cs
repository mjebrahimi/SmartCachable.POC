using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartCachable
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();

            services.AddMemoryCache();
            services.AddSingleton<CacheManager>();
            services.AddSingleton<CacheKeyGenerator>();

            services.AddEntityFrameworkInMemoryDatabase();
            //services.AddEntityFrameworkSqlServer();
            services.AddDbContext<AppDbContext>((sp, opt) => opt
                .UseInternalServiceProvider(sp)
                .UseInMemoryDatabase("SmartCachableDb")
                //.UseSqlServer("Data Source=.;Initial Catalog=SmartCachableDb;Integrated Security=true")
                );

            var serviceProvider = services.BuildServiceProvider();

            var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
            var cacheManager = serviceProvider.GetRequiredService<CacheManager>();

            #region Initialize Data
            if (await dbContext.Businesses.AnyAsync() is false)
            {
                {
                    var business1 = new BusinessEntity
                    {
                        Name = "Business1"
                    };

                    var cat1 = new CategoryEntity
                    {
                        Name = "Business1-Category1",
                        Business = business1,
                        Description = "Business1-Category1-Description",
                        Products = new ProductEntity[]
                        {
                        new ProductEntity
                        {
                            Business = business1,
                            Name = "Business1-Category1-Product1"
                        },
                        new ProductEntity
                        {
                            Business = business1,
                            Name = "Business1-Category1-Product2"
                        }
                        }
                    };

                    var cat2 = new CategoryEntity
                    {
                        Name = "Business1-Category2",
                        Business = business1,
                        Description = "Business1-Category2-Description",
                        Products = new ProductEntity[]
                        {
                        new ProductEntity
                        {
                            Business = business1,
                            Name = "Business1-Category2-Product1"
                        },
                        new ProductEntity
                        {
                            Business = business1,
                            Name = "Business1-Category2-Product2"
                        }
                        }
                    };

                    await dbContext.Businesses.AddAsync(business1);
                    await dbContext.Categories.AddAsync(cat1);
                    await dbContext.Categories.AddAsync(cat2);
                }

                {
                    var business2 = new BusinessEntity
                    {
                        Name = "Business2"
                    };

                    var cat1 = new CategoryEntity
                    {
                        Name = "Business2-Category1",
                        Business = business2,
                        Description = "Business2-Category1-Description",
                        Products = new ProductEntity[]
                        {
                        new ProductEntity
                        {
                            Business = business2,
                            Name = "Business2-Category1-Product1"
                        },
                        new ProductEntity
                        {
                            Business = business2,
                            Name = "Business2-Category1-Product2"
                        }
                        }
                    };

                    var cat2 = new CategoryEntity
                    {
                        Name = "Business2-Category2",
                        Business = business2,
                        Description = "Business2-Category2-Description",
                        Products = new ProductEntity[]
                        {
                        new ProductEntity
                        {
                            Business = business2,
                            Name = "Business2-Category2-Product1"
                        },
                        new ProductEntity
                        {
                            Business = business2,
                            Name = "Business2-Category2-Product2"
                        }
                        }
                    };

                    await dbContext.Businesses.AddAsync(business2);
                    await dbContext.Categories.AddAsync(cat1);
                    await dbContext.Categories.AddAsync(cat2);
                }
                await dbContext.SaveChangesAsync();

                var cat3 = new CategoryEntity
                {
                    Name = "Business2-Category3",
                    Description = "Business2-Category3-Description",
                    BusinessId = 2
                };
                await dbContext.Categories.AddAsync(cat3);
                await dbContext.SaveChangesAsync();

                dbContext.ChangeTracker.Clear();
            }
            #endregion

            var businesses = await dbContext.Businesses.AsNoTracking().ToListAsync();
            var categories = await dbContext.Categories.AsNoTracking().ToListAsync();
            var products = await dbContext.Products.AsNoTracking().ToListAsync();




            //Hit from Database
            var result1 = await cacheManager.GetAsync(
                retrieve: () => dbContext.Categories.AsNoTracking()
                    .Where(p => p.BusinessId == 1)
                    .Select(p => new CategoryReport { Id = p.Id, Name = p.Name, ProductsCount = p.Products.Count })
                    .ToListAsync(),

                keyValues: new { BusinessId = 1 },

                ruleConfig: rules =>
                {
                    rules
                      .AddCacheRule<BusinessEntity>((entity, change) => entity.Id == 1 && change == ChangeKind.Delete)
                      .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1)
                      .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && change != ChangeKind.Update);
                }
            );

            //Hit from Cache
            var result2 = await cacheManager.GetAsync(
                retrieve: () => dbContext.Categories.AsNoTracking()
                    .Where(p => p.BusinessId == 1)
                    .Select(p => new CategoryReport { Id = p.Id, Name = p.Name, ProductsCount = p.Products.Count })
                    .ToListAsync(),

                keyValues: new { BusinessId = 1 },

                ruleConfig: rules =>
                {
                    rules
                      .AddCacheRule<BusinessEntity>((entity, change) => entity.Id == 1 && change == ChangeKind.Delete)
                      .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1)
                      .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && change != ChangeKind.Update);
                }
            );




            //Add a category to business 2
            await dbContext.Categories.AddAsync(new CategoryEntity
            {
                BusinessId = 2,
                Name = "TestCategory"
            });

            //Add a product to business 2
            await dbContext.Products.AddAsync(new ProductEntity
            {
                BusinessId = 2,
                CategoryId = 1,
                Name = "TestProduct"
            });

            //Update a product in business 1
            var updateProduct = await dbContext.Products.Where(p => p.BusinessId == 1).FirstAsync();
            updateProduct.Name += "_Updated";
            dbContext.Products.Update(updateProduct);

            //Remove a product in business 2
            var removeProduct2 = await dbContext.Products.Where(p => p.BusinessId == 2).FirstAsync();
            dbContext.Products.Remove(removeProduct2);

            Console.WriteLine("Non-related entities changed and does not cause cache invalidation." + Environment.NewLine);
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();






            //Hit from Cache
            var result3 = await cacheManager.GetAsync(
                retrieve: () => dbContext.Categories.AsNoTracking()
                    .Where(p => p.BusinessId == 1)
                    .Select(p => new CategoryReport { Id = p.Id, Name = p.Name, ProductsCount = p.Products.Count })
                    .ToListAsync(),

                keyValues: new { BusinessId = 1 },

                ruleConfig: rules =>
                {
                    rules
                      .AddCacheRule<BusinessEntity>((entity, change) => entity.Id == 1 && change == ChangeKind.Delete)
                      .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1)
                      .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && change != ChangeKind.Update);
                }
            );





            //Update a category in business1
            var updateCategory = await dbContext.Categories.Where(p => p.BusinessId == 1).FirstAsync();
            updateCategory.Name += "_Updated";
            dbContext.Categories.Update(updateCategory);

            //Remove Cache
            Console.WriteLine("Related entities changed and cause cache invalidation." + Environment.NewLine);
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();

            //Hit from Database
            var result4 = await cacheManager.GetAsync(
                retrieve: () => dbContext.Categories.AsNoTracking()
                    .Where(p => p.BusinessId == 1)
                    .Select(p => new CategoryReport { Id = p.Id, Name = p.Name, ProductsCount = p.Products.Count })
                    .ToListAsync(),

                keyValues: new { BusinessId = 1 },

                ruleConfig: rules =>
                {
                    rules
                      .AddCacheRule<BusinessEntity>((entity, change) => entity.Id == 1 && change == ChangeKind.Delete)
                      .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1)
                      .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && change != ChangeKind.Update);
                }
            );




            //Remove a product in business1
            var removeProduct1 = await dbContext.Products.Where(p => p.BusinessId == 1).FirstAsync();
            dbContext.Products.Remove(removeProduct1);

            //Remove Cache
            Console.WriteLine("Related entities changed and cause cache invalidation." + Environment.NewLine);
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();


            //Hit from Database
            var result5 = await cacheManager.GetAsync(
                retrieve: () => dbContext.Categories.AsNoTracking()
                    .Where(p => p.BusinessId == 1)
                    .Select(p => new CategoryReport { Id = p.Id, Name = p.Name, ProductsCount = p.Products.Count })
                    .ToListAsync(),

                keyValues: new { BusinessId = 1 },

                ruleConfig: rules =>
                {
                    rules
                      .AddCacheRule<BusinessEntity>((entity, change) => entity.Id == 1 && change == ChangeKind.Delete)
                      .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1)
                      .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && change != ChangeKind.Update);
                }
            );

            //Hit from Cache
            var result6 = await cacheManager.GetAsync(
                retrieve: () => dbContext.Categories.AsNoTracking()
                    .Where(p => p.BusinessId == 1)
                    .Select(p => new CategoryReport { Id = p.Id, Name = p.Name, ProductsCount = p.Products.Count })
                    .ToListAsync(),

                keyValues: new { BusinessId = 1 },

                ruleConfig: rules =>
                {
                    rules
                      .AddCacheRule<BusinessEntity>((entity, change) => entity.Id == 1 && change == ChangeKind.Delete)
                      .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1)
                      .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && change != ChangeKind.Update);
                }
            );

            Console.WriteLine("Done!");
        }

        //static async Task Main(string[] args)
        //{
        //    var services = new ServiceCollection();

        //    services.AddMemoryCache();
        //    services.AddSingleton<CacheManager>();
        //    services.AddSingleton<CacheKeyGenerator>();

        //    services.AddEntityFrameworkInMemoryDatabase();
        //    //services.AddEntityFrameworkSqlServer();
        //    services.AddDbContext<AppDbContext>((sp, opt) => opt
        //        .UseInternalServiceProvider(sp)
        //        .UseInMemoryDatabase("SmartCachableDb")
        //        //.UseSqlServer("Data Source=.;Initial Catalog=SmartCachableDb;Integrated Security=true")
        //        );

        //    var serviceProvider = services.BuildServiceProvider();

        //    var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        //    var cacheManager = serviceProvider.GetRequiredService<CacheManager>();

        //    #region Initialize Data
        //    if (await dbContext.Businesses.AnyAsync() is false)
        //    {
        //        {
        //            var business1 = new BusinessEntity
        //            {
        //                Name = "Business1"
        //            };

        //            var user1 = new UserEntity
        //            {
        //                Name = "Business1-User1",
        //                Business = business1,
        //                IdentityTokens = new IdentityTokenEntity[]
        //                {
        //                new IdentityTokenEntity
        //                {
        //                    Business = business1,
        //                    Token = "Business1-User1-Token1",
        //                },
        //                new IdentityTokenEntity
        //                {
        //                    Business = business1,
        //                    Token = "Business1-User1-Token2"
        //                }
        //                }
        //            };

        //            var user2 = new UserEntity
        //            {
        //                Name = "Business1-User2",
        //                Business = business1,
        //                IdentityTokens = new IdentityTokenEntity[]
        //                {
        //                new IdentityTokenEntity
        //                {
        //                    Business = business1,
        //                    Token = "Business1-User2-Token1"
        //                },
        //                new IdentityTokenEntity
        //                {
        //                    Business = business1,
        //                    Token = "Business1-User2-Token2"
        //                }
        //                }
        //            };

        //            var cat1 = new CategoryEntity
        //            {
        //                Name = "Business1-Category1",
        //                Business = business1,
        //                Description = "Business1-Category1-Description",
        //                Products = new ProductEntity[]
        //                {
        //                new ProductEntity
        //                {
        //                    Business = business1,
        //                    Name = "Business1-Category1-Product1"
        //                },
        //                new ProductEntity
        //                {
        //                    Business = business1,
        //                    Name = "Business1-Category1-Product2"
        //                }
        //                }
        //            };

        //            var cat2 = new CategoryEntity
        //            {
        //                Name = "Business1-Category2",
        //                Business = business1,
        //                Description = "Business1-Category2-Description",
        //                Products = new ProductEntity[]
        //                {
        //                new ProductEntity
        //                {
        //                    Business = business1,
        //                    Name = "Business1-Category2-Product1"
        //                },
        //                new ProductEntity
        //                {
        //                    Business = business1,
        //                    Name = "Business1-Category2-Product2"
        //                }
        //                }
        //            };

        //            await dbContext.Businesses.AddAsync(business1);
        //            await dbContext.Users.AddAsync(user1);
        //            await dbContext.Users.AddAsync(user2);
        //            await dbContext.Categories.AddAsync(cat1);
        //            await dbContext.Categories.AddAsync(cat2);
        //        }

        //        {
        //            var business2 = new BusinessEntity
        //            {
        //                Name = "Business2"
        //            };

        //            var user1 = new UserEntity
        //            {
        //                Name = "Business2-User1",
        //                Business = business2,
        //                IdentityTokens = new IdentityTokenEntity[]
        //                {
        //                new IdentityTokenEntity
        //                {
        //                    Business = business2,
        //                    Token = "Business2-User1-Token1",
        //                },
        //                new IdentityTokenEntity
        //                {
        //                    Business = business2,
        //                    Token = "Business2-User1-Token2"
        //                }
        //                }
        //            };

        //            var user2 = new UserEntity
        //            {
        //                Name = "Business2-User2",
        //                Business = business2,
        //                IdentityTokens = new IdentityTokenEntity[]
        //                {
        //                new IdentityTokenEntity
        //                {
        //                    Business = business2,
        //                    Token = "Business2-User2-Token1"
        //                },
        //                new IdentityTokenEntity
        //                {
        //                    Business = business2,
        //                    Token = "Business2-User2-Token2"
        //                }
        //                }
        //            };

        //            var cat1 = new CategoryEntity
        //            {
        //                Name = "Business2-Category1",
        //                Business = business2,
        //                Description = "Business2-Category1-Description",
        //                Products = new ProductEntity[]
        //                {
        //                new ProductEntity
        //                {
        //                    Business = business2,
        //                    Name = "Business2-Category1-Product1"
        //                },
        //                new ProductEntity
        //                {
        //                    Business = business2,
        //                    Name = "Business2-Category1-Product2"
        //                }
        //                }
        //            };

        //            var cat2 = new CategoryEntity
        //            {
        //                Name = "Business2-Category2",
        //                Business = business2,
        //                Description = "Business2-Category2-Description",
        //                Products = new ProductEntity[]
        //                {
        //                new ProductEntity
        //                {
        //                    Business = business2,
        //                    Name = "Business2-Category2-Product1"
        //                },
        //                new ProductEntity
        //                {
        //                    Business = business2,
        //                    Name = "Business2-Category2-Product2"
        //                }
        //                }
        //            };



        //            await dbContext.Businesses.AddAsync(business2);
        //            await dbContext.Users.AddAsync(user1);
        //            await dbContext.Users.AddAsync(user2);
        //            await dbContext.Categories.AddAsync(cat1);
        //            await dbContext.Categories.AddAsync(cat2);
        //        }
        //        await dbContext.SaveChangesAsync();

        //        var cat3 = new CategoryEntity
        //        {
        //            Name = "Business2-Category3",
        //            Description = "Business2-Category3-Description",
        //            BusinessId = 2
        //        };
        //        await dbContext.Categories.AddAsync(cat3);
        //        await dbContext.SaveChangesAsync();

        //        dbContext.ChangeTracker.Clear();
        //    }
        //    #endregion

        //    var businesses = await dbContext.Businesses.AsNoTracking().ToListAsync();
        //    var users = await dbContext.Users.AsNoTracking().ToListAsync();
        //    var identityTokens = await dbContext.IdentityTokens.AsNoTracking().ToListAsync();
        //    var categories = await dbContext.Categories.AsNoTracking().ToListAsync();
        //    var products = await dbContext.Products.AsNoTracking().ToListAsync();






        //    //Hit from Database
        //    var result1 = await cacheManager.GetAsync(
        //        retrieve: () => dbContext.Products.AsNoTracking()
        //            .Where(p => p.BusinessId == 1 && p.CategoryId == 2)
        //            .ToListAsync(),

        //        keyValues: new { BusinessId = 1, CategoryId = 2 },

        //        ruleConfig: rules =>
        //        {
        //            rules
        //              .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && entity.CategoryId == 2)
        //              .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1 && entity.Id == 2 && (change == ChangeKind.Update || change == ChangeKind.Delete));
        //        }
        //    );

        //    //Hit from Cache
        //    var result2 = await cacheManager.GetAsync(
        //        retrieve: () => dbContext.Products.AsNoTracking()
        //            .Where(p => p.BusinessId == 1 && p.CategoryId == 2)
        //            .ToListAsync(),

        //        keyValues: new { BusinessId = 1, CategoryId = 2 },

        //        ruleConfig: rules =>
        //        {
        //            rules
        //              .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && entity.CategoryId == 2)
        //              .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1 && entity.Id == 2 && (change == ChangeKind.Update || change == ChangeKind.Delete));
        //        }
        //    );






        //    //BusinessId-1_CategoryId=1
        //    await dbContext.Products.AddAsync(new ProductEntity
        //    {
        //        BusinessId = 1,
        //        CategoryId = 1,
        //        Name = "TestProduct1"
        //    });
        //    //BusinessId-2_CategoryId=2
        //    await dbContext.Products.AddAsync(new ProductEntity
        //    {
        //        BusinessId = 2,
        //        CategoryId = 2,
        //        Name = "TestProduct2"
        //    });
        //    //BusinessId-2_CategoryId=1
        //    await dbContext.Products.AddAsync(new ProductEntity
        //    {
        //        BusinessId = 2,
        //        CategoryId = 1,
        //        Name = "TestProduct3"
        //    });

        //    //Update Category4
        //    var category4 = await dbContext.Categories.FindAsync(4);
        //    category4.Name += "_Updated";
        //    dbContext.Categories.Update(category4);

        //    //Remove Category5
        //    var category5 = await dbContext.Categories.FindAsync(5);
        //    dbContext.Categories.Remove(category5);

        //    //Add another Category
        //    await dbContext.Categories.AddAsync(new CategoryEntity
        //    {
        //        BusinessId = 1,
        //        Name = "CategoryTest",
        //        Description = "DescriptionTest"
        //    });

        //    await dbContext.SaveChangesAsync();
        //    dbContext.ChangeTracker.Clear();






        //    //Hit from Cache
        //    var result3 = await cacheManager.GetAsync(
        //        retrieve: () => dbContext.Products.AsNoTracking()
        //            .Where(p => p.BusinessId == 1 && p.CategoryId == 2)
        //            .ToListAsync(),

        //        keyValues: new { BusinessId = 1, CategoryId = 2 },

        //        ruleConfig: rules =>
        //        {
        //            rules
        //              .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && entity.CategoryId == 2)
        //              .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1 && entity.Id == 2 && (change == ChangeKind.Update || change == ChangeKind.Delete));
        //        }
        //    );





        //    //Update Category2
        //    var category2 = await dbContext.Categories.Include(p => p.Products).Where(p => p.Id == 2).FirstAsync();
        //    category2.Name += "_Updated";
        //    dbContext.Categories.Update(category2);

        //    //Remove Cache
        //    await dbContext.SaveChangesAsync();

        //    //Hit from Database
        //    var result4 = await cacheManager.GetAsync(
        //        retrieve: () => dbContext.Products.AsNoTracking()
        //            .Where(p => p.BusinessId == 1 && p.CategoryId == 2)
        //            .ToListAsync(),

        //        keyValues: new { BusinessId = 1, CategoryId = 2 },

        //        ruleConfig: rules =>
        //        {
        //            rules
        //              .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && entity.CategoryId == 2)
        //              .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1 && entity.Id == 2 && (change == ChangeKind.Update || change == ChangeKind.Delete));
        //        }
        //    );

        //    //Hit from Cache
        //    var result5 = await cacheManager.GetAsync(
        //        retrieve: () => dbContext.Products.AsNoTracking()
        //            .Where(p => p.BusinessId == 1 && p.CategoryId == 2)
        //            .ToListAsync(),

        //        keyValues: new { BusinessId = 1, CategoryId = 2 },

        //        ruleConfig: rules =>
        //        {
        //            rules
        //              .AddCacheRule<ProductEntity>((entity, change) => entity.BusinessId == 1 && entity.CategoryId == 2)
        //              .AddCacheRule<CategoryEntity>((entity, change) => entity.BusinessId == 1 && entity.Id == 2 && (change == ChangeKind.Update || change == ChangeKind.Delete));
        //        }
        //    );

        //    //Remove Category2
        //    dbContext.Products.RemoveRange(category2.Products);
        //    dbContext.Categories.Remove(category2);

        //    //Remove Cache
        //    await dbContext.SaveChangesAsync();

        //    Console.WriteLine("Done!");
        //}
    }

    public class CategoryReport
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ProductsCount { get; set; }
    }
}
