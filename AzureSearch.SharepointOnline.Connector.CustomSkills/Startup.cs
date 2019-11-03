//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureSearch.SharepointOnline.Connector.CustomSkills.Config;
using BishopBlobCustomSkill.Config;
using BishopBlobCustomSkill.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BishopBlobCustomSkill
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.Configure<ConnectionStringsConfig>(this.Configuration.GetSection("ConnectionStrings"));
            services.Configure<AppSettingsEnvironmentConfig>(this.Configuration.GetSection("EnvironmentConfig"));
            services.Configure<EnvironmentConfig>(this.Configuration);
            services.AddSingleton<ISharePointMetadataService, SharePointMetadataService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
