using Microsoft.AspNetCore.Mvc;
using VaultShop.Utility;
using VaultShop.DataAccess.Data;
using Microsoft.EntityFrameworkCore;

namespace VaultShop.Controllers
{
    public class SeoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SeoController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Route("robots.txt")]
        public IActionResult Robots()
        {
            var content = "User-agent: *\n" +
                "Allow: /\n" +
                "Disallow: /Identity/\n" +
                "Disallow: /Admin/\n" +
                "Disallow: /Cart/\n" +
                "Disallow: /Favorite/\n\n" +
                "Sitemap: " + SD.SiteUrl + "/sitemap.xml";
            return Content(content, "text/plain");
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = SD.SiteUrl;
            var culture = "es-AR";

            var categories = await _context.Categories.ToListAsync();
            var products = await _context.Products.Where(p => !p.IsDeleted).ToListAsync();

            var staticPages = new (string Url, string Priority, string Changefreq)[]
            {
                (Url: $"{baseUrl}/{culture}/", Priority: "1.0", Changefreq: "daily"),
                (Url: $"{baseUrl}/{culture}/Home/AboutUs", Priority: "0.8", Changefreq: "monthly"),
                (Url: $"{baseUrl}/{culture}/Home/FAQs", Priority: "0.8", Changefreq: "monthly"),
                (Url: $"{baseUrl}/{culture}/Home/Privacy", Priority: "0.7", Changefreq: "monthly"),
                (Url: $"{baseUrl}/{culture}/Home/TakeCare", Priority: "0.7", Changefreq: "monthly"),
                (Url: $"{baseUrl}/{culture}/Home/Search", Priority: "0.9", Changefreq: "weekly"),
            };

            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";

            foreach (var page in staticPages)
            {
                xml += $"<url>\n<loc>{page.Url}</loc>\n<changefreq>{page.Changefreq}</changefreq>\n<priority>{page.Priority}</priority>\n</url>\n";
            }

            foreach (var category in categories)
            {
                xml += $"<url>\n<loc>{baseUrl}/{culture}/Home/Search?category={category.Id}</loc>\n<changefreq>weekly</changefreq>\n<priority>0.8</priority>\n</url>\n";
            }

            foreach (var product in products)
            {
                xml += $"<url>\n<loc>{baseUrl}/{culture}/Home/Details/{product.Id}</loc>\n<changefreq>weekly</changefreq>\n<priority>0.9</priority>\n</url>\n";
            }

            xml += "</urlset>";

            return Content(xml, "application/xml");
        }
    }
}