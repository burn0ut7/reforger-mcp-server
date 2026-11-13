using System.Security.Claims;
using System.Text.Json;
using GC.Web;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.AddAuthorization(o => o.AddPolicy(SiteNavigation.AdminPolicy, p => p.RequireRole("Developer")));
await using var app = builder.Build();
app.Use(async (context, next) => {
 if (context.Request.Headers.ContainsKey("Test-Signed-In")) context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,"test")},"test"));
 await next(context);
});
SiteNavigation.Map(app);
await app.StartAsync();
using var client = new HttpClient{BaseAddress=new Uri(app.Urls.Single())};
foreach (var signedIn in new[]{false,true}) {
 client.DefaultRequestHeaders.Clear();
 if(signedIn) client.DefaultRequestHeaders.Add("Test-Signed-In","true");
 using var response = await client.GetAsync("/api/navigation");
 response.EnsureSuccessStatusCode();
 using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
 var items=json.RootElement;
 var expected=signedIn?"Account":"Sign in";
 if(items[0].GetProperty("label").GetString()!="Home" || items[1].GetProperty("label").GetString()!=expected || items[1].GetProperty("href").GetString()!="/account.html" || response.Headers.CacheControl?.NoStore!=true) throw new Exception("Navigation mismatch");
 Console.WriteLine($"PASS: {(signedIn?"signed in":"signed out")} shows Home then {expected}, same account destination, no-store.");
}
await app.StopAsync();
