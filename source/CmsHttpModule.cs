using System;
using System.Configuration;
using System.Web;
using Telerik.Cms.Web;

namespace Alanta.Sitefinity
{
  public class CmsHttpModule : IHttpModule
   {
      private Telerik.Cms.Web.CmsHttpModule _cmsHttpModule;

      public void Init(HttpApplication context)
      {
         _cmsHttpModule = HttpRuntime.UsingIntegratedPipeline ? new CmsHttModuleClassicModeProxy( this ) : (Telerik.Cms.Web.CmsHttpModule) new CmsHttModuleIntegratedModeProxy( this );
         _cmsHttpModule.Init( context );

         context.Error += Error;
         context.PostRequestHandlerExecute += PostRequestHandlerExecute;
      }

      public void Dispose()
      {
         if( null != _cmsHttpModule )
         {
            _cmsHttpModule.Dispose();
         }
      }

      protected Telerik.Cms.Web.CmsRequest GetCmsRequest( System.Web.HttpContext context )
      {
         return new CmsRequest( CmsHttpModuleHelper.TruncateUrl( context.Request.RawUrl, UrlHelper.PageExtension ) );
      }

      protected string GetUrl( HttpContext context )
      {
         string rawUrl = CmsHttpModuleHelper.TruncateUrl( context.Request.Url.UnescapedPathAndQuery(), UrlHelper.PageExtension );

         if ( RewritingEnabled )
         {
            AdvancedUrlRewriter.IRewriteRule rule = AdvancedUrlRewriter.Rewriter.FindMatch( rawUrl );
            if ( rule != null )
            {
               rawUrl = rule.Execute( rawUrl );
               if ( rule.Mode == AdvancedUrlRewriter.RewriteMode.Rewrite )
               {
                  return rawUrl;
               }
               Redirect( context.Response, rawUrl, rule.Mode == AdvancedUrlRewriter.RewriteMode.PermanentRedirect );
            }
         }
         if ( !string.Equals( CmsHttpModuleHelper.TruncateUrl( context.Request.RawUrl, UrlHelper.PageExtension ), rawUrl, StringComparison.OrdinalIgnoreCase ) )
         {
            return rawUrl;
         }

         return string.Empty;
      }

      private void Redirect( HttpResponse response, string url, bool permanent )
      {
         if ( permanent )
         {
            response.Status = "301 Moved Permanently";
            response.AddHeader( "Location", UrlPath.ResolveUrl( url ) );
            response.End();
         }
         else
         {
            response.Redirect( UrlPath.ResolveUrl( url ), true );
         }
      }

      private bool RewritingEnabled
      {
         get
         {
            if ( !_rewritingEnabledEvaluated )
            {
               _rewritingEnabled = ConfigurationManager.GetSection( "telerik/urlrewrites" ) != null;
               _rewritingEnabledEvaluated = true;
            }
            return _rewritingEnabled;
         }
      }

      private bool _rewritingEnabledEvaluated;
      private bool _rewritingEnabled;

      private void PostRequestHandlerExecute( object sender, EventArgs e )
      {
         var context = HttpContext.Current;
         // Set the error code passed in the headers when TransferRequest was invoked.
         var error = context.Request.Headers["__sf__error"];
         if ( null != error && context.Response.StatusCode == 200 )
         {
            int errorCode;
            if ( Int32.TryParse( error, out errorCode ) )
            {
               context.Response.StatusCode = errorCode;
               context.Response.TrySkipIisCustomErrors = true;
            }
         }
      }

      private void Error( object sender, EventArgs e )
      {
         var context = HttpContext.Current;

         // This example only handle 404 errors
         // You could also add some similar logic for 500 internal server
         // errors (logic errors) and do some logging
         var error = context.Server.GetLastError() as HttpException;
         if ( null != error && error.GetHttpCode() == 404 )
         {
            // we can still use the web.config custom errors information to
            // decide whether to redirect
            var config = ( CustomErrorsSection )WebConfigurationManager.GetSection( "system.web/customErrors" );
            if ( config.Mode == CustomErrorsMode.On ||
                 ( config.Mode == CustomErrorsMode.RemoteOnly && !context.Request.IsLocal ) )
            {
               // redirect to the 404 error page from the web.config
               if ( config.Errors["404"] != null )
               {
                  if ( HttpRuntime.UsingIntegratedPipeline )
                  {
                     context.Server.TransferRequest( config.Errors["404"].Redirect, true, "GET",
                                                     new NameValueCollection { { "__sf__error", "404" } } );
                  }
                  else
                  {
                     var context404 = CmsSiteMap.Provider.FindSiteMapNode( config.Errors["404"].Redirect ) as CmsSiteMapNode;
                     if ( null != context404 )
                     {
                        context.Server.ClearError();
                        context.Response.TrySkipIisCustomErrors = true;
                        context.Response.StatusCode = 404;
                        CmsUrlContext.Current = context404;
                        context.Items["cmspageid"] = context404.PageID;
                        context.Server.Transfer( "~/sitefinity/cmsentrypoint.aspx" );
                     }
                  }
               }
               else
               {
                  context.Server.TransferRequest( config.DefaultRedirect );
               }
            }
         }
      }



      private class CmsHttModuleIntegratedModeProxy : Telerik.Cms.Web.CmsHttpModuleIIS7Integrated
      {
         private readonly CmsHttpModule _owner;

         public CmsHttModuleIntegratedModeProxy( CmsHttpModule owner )
         {
            _owner = owner;
         }

         protected override string GetUrl( HttpContext context )
         {
            return _owner.GetUrl( context );
         }

         protected override Telerik.Cms.Web.CmsRequest GetCmsRequest( System.Web.HttpContext context )
         {
            return _owner.GetCmsRequest( context );
         }
      }

      private class CmsHttModuleClassicModeProxy : Telerik.Cms.Web.CmsHttpModule
      {
         private readonly CmsHttpModule _owner;

         public CmsHttModuleClassicModeProxy( CmsHttpModule owner )
         {
            _owner = owner;
         }

         protected override string GetUrl( HttpContext context )
         {
            return _owner.GetUrl( context );
         }

         protected override Telerik.Cms.Web.CmsRequest GetCmsRequest( System.Web.HttpContext context )
         {
            return _owner.GetCmsRequest( context );
         }
      }
   }

   public static class CmsHttpModuleHelper
   {
      public static string UnescapedPathAndQuery( this Uri uri )
      {
         var parts = uri.PathAndQuery.Split( new[] { '?' }, StringSplitOptions.RemoveEmptyEntries );

         var result = Uri.UnescapeDataString( parts[0] );
         if ( parts.Length > 1 )
         {
            result += "?" + parts[1];
         }
         return result;
      }

      public static string TruncateUrl( string rawUrl, string pageExtension )
      {
         var queryString = rawUrl.IndexOf( "?" ) > 0 ? rawUrl.Substring( rawUrl.IndexOf( "?" ) ) : string.Empty;
         var loweredUrl = rawUrl.ToLowerInvariant();
         var index = loweredUrl.IndexOf( pageExtension.ToLower() );
         if ( index > 0 && ( queryString.Length == 0 || index < loweredUrl.IndexOf( "?" ) ) && index != loweredUrl.LastIndexOf( "." ) )
         {
            return rawUrl.Substring( 0, index + pageExtension.Length ) + queryString;
         }
         return rawUrl;
      }
   }
}