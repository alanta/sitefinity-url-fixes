Replacement CMS Http module for Sitefinity 3.7
===================================

Sitefinity 3.7 has some issues handling url's. This replacement CmsHttpModule fixes the following issues:
 * PathInfo (path data after .aspx) is accepted (by default Sitefinity breaks on path info in a URL)
 * Enable IIS-based URL rewriting
 * Return proper 404 HTTP status result when 404 error occurs, without redirecting


Usage
--------
Put this code in an assembly or in App_Code. Replace the Cms module declarations in web.config with the supplied code.
This module will check whether URL rewriting rules for Sitefinity's Advanced URL rewriter are declared in the web.config file. If so Sitefinity based URL rewriting is used *after* IIS based rewriting. The module is suitable for both classic ASP.NET mode and IIS7's Integrated Pipeline mode. It's also fully compatible with the Visual Studio development web server.