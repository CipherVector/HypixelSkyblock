using System;
using System.Net;
using AspNetCoreRateLimit;
using AspNetCoreRateLimit.Redis;
using hypixel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Prometheus;
using StackExchange.Redis;

namespace dev
{
    public class Startup
    {
        private IConfiguration Configuration;
        public Startup(IConfiguration conf)
        {
            Configuration = conf;
        }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            var redisCon = SimplerConfig.Config.Instance["redisCon"];
            services.AddControllers().AddNewtonsoftJson();
            services.AddSwaggerGen();
            services.AddSwaggerGenNewtonsoftSupport();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisCon;
                options.InstanceName = "SampleInstance";
            });
            services.AddResponseCaching();

            services.AddDbContext<HypixelContext>();
            services.AddSingleton<AuctionService>(AuctionService.Instance);

            var redisOptions = ConfigurationOptions.Parse(redisCon);
            services.AddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect(redisOptions));
            services.AddJaeger();

            // Rate limiting 
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            services.AddRedisRateLimiting();
            services.AddSingleton<IIpPolicyStore, DistributedCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, DistributedCacheRateLimitCounterStore>();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                c.RoutePrefix = "api";
            });

            app.UseRouting();


            app.UseResponseCaching();
            app.UseIpRateLimiting();


            app.Use(async (context, next) =>
            {
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(10)
                    };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
                    new string[] { "Accept-Encoding" };

                await next();
            });

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; ;
                    context.Response.ContentType = "text/json";

                    var exceptionHandlerPathFeature =
                        context.Features.Get<IExceptionHandlerPathFeature>();

                    if (exceptionHandlerPathFeature?.Error is CoflnetException ex)
                    {
                        await context.Response.WriteAsync(
                                        JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));
                    }
                    else
                    {
                        await context.Response.WriteAsync(
                                        JsonConvert.SerializeObject(new { Slug = "internal_error", Message = "An unexpectedinternal error occured. Please check that your request is valid." }));
                    }
                });
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var path = context.Request.Path;
                    //context.Response.WriteAsync()
                    //await hypixel.Program.server.AnswerGetRequest();
                    await context.Response.WriteAsync("Hello World!");
                });
                endpoints.MapMetrics();
                endpoints.MapControllers();
            });
        }
    }
}
