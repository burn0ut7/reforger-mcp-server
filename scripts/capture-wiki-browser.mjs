// Operator-side capture helper. It uses an already connected, documented browser tab.
// No browser launch, HTTP client, cookie access, challenge handling, or search backend.
import * as fs from "node:fs/promises";
import { createHash } from "node:crypto";

export async function createWikiCapture(tab, directory) {
    await fs.mkdir(directory); // Fresh snapshot only; never overwrite an earlier capture.
    const root = "https://community.bistudio.com/wiki/Category:Arma_Reforger/Modding";
    const index = {
        schema: 1,
        startedAt: new Date().toISOString(),
        method: "browser rendered DOM; ordinary navigation; no challenge automation",
        scope: "Pages reached from the Arma Reforger Modding category: all observed slash-descendant Modding categories and linked Arma Reforger namespace articles. Other namespaces, media files and external sites are inventoried as links only; no claim of all uncategorized documentation.",
        pages: [], failures: [], queue: [root]
    };
    const seen = new Set();
    let needsAttention = false;
    const hash = text => createHash("sha256").update(text).digest("hex");
    const save = () => fs.writeFile(directory + "/index.json", JSON.stringify(index, null, 2));
    await save();
    return {
        async next(maxPages = 10) {
            if (needsAttention) throw new Error("Capture stopped for inspection; do not automatically retry a challenge or failed page.");
            if (!Number.isInteger(maxPages) || maxPages < 1 || maxPages > 50) throw new Error("maxPages must be 1-50.");
            for (let i = 0; i < maxPages && index.queue.length; i++) {
                const requestedUrl = index.queue.shift();
                if (seen.has(requestedUrl)) { i--; continue; }
                seen.add(requestedUrl);
                try {
                    await tab.goto(requestedUrl);
                    const observed = await tab.playwright.evaluate(() => {
                        const html = document.documentElement.outerHTML;
                        const content = document.querySelector("#mw-content-text");
                        const contentHtml = content?.innerHTML ?? "";
                        return {
                            url: location.href, title: document.title,
                            htmlLength: html.length, contentLength: contentHtml.length,
                            htmlChunks: html.match(/[\s\S]{1,60000}/g) ?? [],
                            contentChunks: contentHtml.match(/[\s\S]{1,60000}/g) ?? [],
                            revisionUrl: document.querySelector("#t-permalink a")?.href ?? null,
                            lastModified: document.querySelector("#footer-info-lastmod")?.textContent ?? null,
                            links: Array.from(document.querySelectorAll("#mw-content-text a[href],#mw-normal-catlinks a[href]"))
                                .map(a => ({ title: a.getAttribute("title"), text: a.textContent, url: a.href }))
                        };
                    });
                    const html = observed.htmlChunks.join("");
                    const contentHtml = observed.contentChunks.join("");
                    if (html.length !== observed.htmlLength || contentHtml.length !== observed.contentLength ||
                        !contentHtml || !html.endsWith("</html>"))
                        throw new Error("Incomplete DOM or missing wiki content: " + observed.title);
                    const { htmlChunks, contentChunks, ...metadata } = observed;
                    const file = hash(requestedUrl).slice(0, 20) + ".json";
                    const page = { ...metadata, requestedUrl, html, contentHtml,
                        capturedAt: new Date().toISOString(), htmlSha256: hash(html) };
                    await fs.writeFile(directory + "/" + file, JSON.stringify(page, null, 2));
                    index.pages.push({ url: page.url, requestedUrl, title: page.title, file,
                        capturedAt: page.capturedAt, htmlSha256: page.htmlSha256, links: page.links,
                        revisionUrl: page.revisionUrl, lastModified: page.lastModified });
                    for (const link of page.links) {
                        const u = new URL(link.url);
                        const path = decodeURIComponent(u.pathname).replaceAll("_", " ");
                        if (u.origin !== "https://community.bistudio.com") continue;
                        const included = path.startsWith("/wiki/Arma Reforger:") ||
                            path === "/wiki/Category:Arma Reforger/Modding" ||
                            path.startsWith("/wiki/Category:Arma Reforger/Modding/");
                        if (!included) continue;
                        if (/[?&](pagefrom|subcatfrom|filefrom)=/.test(u.search))
                            throw new Error("Category pagination requires explicit inspection: " + u.href);
                        if (u.search) continue;
                        u.hash = "";
                        if (!seen.has(u.href) && !index.queue.includes(u.href)) index.queue.push(u.href);
                    }
                } catch (error) {
                    needsAttention = true;
                    index.failures.push({ url: requestedUrl, error: String(error) });
                    await save();
                    throw error;
                }
                await save();
            }
            if (!index.queue.length && !index.failures.length) {
                index.completedAt = new Date().toISOString();
                await save();
            }
            return { captured: index.pages.length, remaining: index.queue.length,
                needsAttention, completedAt: index.completedAt ?? null, directory };
        }
    };
}
