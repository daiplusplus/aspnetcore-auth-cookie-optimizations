using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jehoel
{
	public static class OptimizedTicketDataFormatExtensions
	{
		public static IServiceCollection AddOptimizedTicketDataFormat( this IServiceCollection services )
		{
			return services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>,OptimizedPostConfigureCookieAuthenticationOptions>();
		}
	}

	public class OptimizedPostConfigureCookieAuthenticationOptions : IPostConfigureOptions<CookieAuthenticationOptions>
    {
        private readonly IDataProtectionProvider dp;
		private readonly ILoggerFactory          loggerFactory;

		// These strings are absolutely required:
		private const String _LegacyPurpose1 = "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware";
		private const String _LegacyPurpose2 = "v2";

        public OptimizedPostConfigureCookieAuthenticationOptions(IDataProtectionProvider dataProtection, ILoggerFactory loggerFactory)
        {
            this.dp            = dataProtection ?? throw new ArgumentNullException( nameof(dataProtection) );
			this.loggerFactory = loggerFactory  ?? throw new ArgumentNullException( nameof(loggerFactory) );
        }

		public void PostConfigure( String name, CookieAuthenticationOptions options )
		{
			// It doesn't matter if this `IPostConfigureOptions<CookieAuthenticationOptions>` runs before or after ASP.NET Core's built-in `PostConfigureCookieAuthenticationOptions` class.
			// ...because it only overwrites `options.TicketDataFormat` if it's null. And we can't set `options.TicketDataFormat = new OptimizedTicketDataFormat()` because we need the `IDataProtectionProvider` which we can't get in AddCookies's options method.
			// And `PostConfigureCookieAuthenticationOptions` doesn't have any custom logic for getting `IDataProtectionProvider` (it's "use CookieAuthenticationOptions or use our own" too. (`this.dp == PostConfigureCookieAuthenticationOptions._dp` too)

//			if( options.TicketDataFormat == null ) throw new InvalidOperationException( "The default Cookie Authentication IPostConfigureOptions has not run." );

			options.DataProtectionProvider = options.DataProtectionProvider ?? this.dp;

			IDataProtector dataProtector = options.DataProtectionProvider.CreateProtector( _LegacyPurpose1, name, _LegacyPurpose2 );

			options.TicketDataFormat = new OptimizedTicketDataFormat( this.loggerFactory, dataProtector );

			//

			if( OptimizedTicketDataFormat.ChunkSize != null )
			{
				if( options.CookieManager == null )
				{
					options.CookieManager = new ChunkingCookieManager() { ChunkSize = OptimizedTicketDataFormat.ChunkSize.Value };
				}
				else if( options.CookieManager is ChunkingCookieManager ccm )
				{
					ccm.ChunkSize = OptimizedTicketDataFormat.ChunkSize.Value;
				}
			}
		}
	}
}
