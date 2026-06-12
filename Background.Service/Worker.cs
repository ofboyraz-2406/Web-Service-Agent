using System;
using System.Collections.Generic; 
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Background.service.Models;
using Background.Service.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using ExcelDataReader;
using DocumentFormat.OpenXml.InkML;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Background.service;

public class Worker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public Worker(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }



    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var http = _httpClientFactory.CreateClient();

                var schedules = await http.GetFromJsonAsync<List<ScheduleManager>>(
                    "http://localhost:5000/api/schedulemanager",
                    stoppingToken
                ) ?? new List<ScheduleManager>();

                var now = DateTime.Now;
                var currentTime = now.ToString("HH:mm");

                var dueSchedules = schedules.Where(s =>
                {
                    if (s.IsProcessing == 1) return false;
                    if (string.IsNullOrWhiteSpace(s.StartTime) || string.IsNullOrWhiteSpace(s.EndTime)) return false;
                    if (string.Compare(currentTime, s.StartTime) < 0 || string.Compare(currentTime, s.EndTime) > 0) return false;

                    if (!string.IsNullOrWhiteSpace(s.ProcessedAt))
                    {
                        if (DateTime.TryParseExact(s.ProcessedAt, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var lastRun))
                        {
                            var elapsed = (now - lastRun).TotalMinutes;
                            if (s.PeriodType?.ToUpper() == "SA") elapsed = (now - lastRun).TotalHours;
                            if (elapsed < s.ScheduledPeriod) return false;
                        }
                    }
                    return true;
                }).ToList();

                var allDefs = await http.GetFromJsonAsync<List<Definition>>(
                    "http://localhost:5000/api/definitions",
                    stoppingToken
                ) ?? new List<Definition>();

                var defs = allDefs
                    .Where(x => x.IsActive == 1 && dueSchedules.Any(s => s.DefinitionId == x.Id))
                    .OrderBy(x => x.Id)
                    .ToList();

                Console.WriteLine($"\n--- DUE DEFINITIONS CHECK ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ---");

                if (defs.Count == 0)
                {
                    Console.WriteLine("Çalışacak definition yok.");
                }
                else
                {
                    foreach (var d in defs)
                    {
                        Console.WriteLine(
                            $"Id={d.Id} | {d.ServiceType} | {d.Name} | {d.Endpoint} | {d.MethodName} | Table={d.TableName}"
                        );
                    }

                    var tasks = new List<Task>();

                    foreach (var def in defs)
                    {
                        if (def.MainDefinitionId.HasValue)
                        {
                            tasks.Add(CallChainedService(defs, def, stoppingToken));
                        }
                        else
                        {
                            if (def.ServiceType == "REST")
                            {
                                tasks.Add(CallRestService(def, stoppingToken));
                            }
                            else if (def.ServiceType == "SOAP")
                            {
                                tasks.Add(CallSoapService(def, stoppingToken));
                            }
                            else if (def.ServiceType == "WEB")
                            {
                                tasks.Add(CallWebService(def, stoppingToken));
                            }
                            else if (def.ServiceType== "RPA")
                            {
                                tasks.Add(GetHtmlWithRpa(def.Endpoint, def));
                            }
                        }
                    }

                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scheduler ERROR: {ex.Message}");
            }

            Console.WriteLine("1 dakika bekleniyor...");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }


    private async Task CallWebService(Definition def, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9," +
            "image/avif,image/webp,image/apng,*/*;q=0.8");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");

        try
        {
            Console.WriteLine($"WEB SERVİSİ ÇALIŞIYOR: {def.Name}...");
            await MarkDefinitionStart(def.Id, ct);

            string responseText = "";
            bool isSuccess = false;

            if (!string.IsNullOrEmpty(def.MethodName) && def.MethodName == "GetHtmlWithRpa")
            {
                Console.WriteLine("STARTOK");
                responseText = await GetHtmlWithRpa(def.Endpoint, def);
                Console.WriteLine($"WEBOK? Uzunluk: {responseText?.Length}");
                isSuccess = !string.IsNullOrWhiteSpace(responseText) && responseText.Length >= 100;

                if ((_dosyadanGelenVeriler != null && _dosyadanGelenVeriler.Count > 0) || _multitables.Count > 0)
                {
                    if (_multitables.Count > 0)
                    {
                        foreach (var tablo in _multitables)
                        {
                            Console.WriteLine($"[CSV] {tablo.Key} tablosu yazılıyor → {tablo.Value.Count} satır");
                            await PostDataToWebService(def.Id, tablo.Key, tablo.Value, ct);
                        }
                        _multitables.Clear();
                    }
                    else
                    {
                        await PostDataToWebService(def.Id, def.TableName, _dosyadanGelenVeriler, ct);
                    }

                    await SendOperationAsync(def.Id, "WEB_CALL", def.Endpoint, "Dosyadan veri çekildi", "SUCCESS", ct);
                    await MarkDefinitionComplete(def.Id, ct);
                    Console.WriteLine($"WEB OK → {def.TableName}");
                    _dosyadanGelenVeriler = new List<Dictionary<string, object?>>();
                    return;
                }

            }
            else
            {
                int maxTry = 3;
                for (int attempt = 1; attempt <= maxTry; attempt++)
                {
                    HttpMethod method = (!string.IsNullOrWhiteSpace(def.MethodName) && def.MethodName.ToUpper() == "POST")
                                        ? HttpMethod.Post : HttpMethod.Get;

                    using var req = new HttpRequestMessage(method, def.Endpoint);

                    Console.WriteLine($"[DEBUG] DefId={def.Id} | ParseType={def.ParseType} | Method={method} | Body={def.RequestBody}");

                    if (method == HttpMethod.Post && !string.IsNullOrWhiteSpace(def.RequestBody))
                    {
                        if (def.ParseType?.ToUpper() == "FORM")
                        {
                            req.Content = new FormUrlEncodedContent(
                                def.RequestBody
                                    .Split('&')
                                    .Select(pair => pair.Split('='))
                                    .Where(parts => parts.Length == 2)
                                    .Select(parts => new KeyValuePair<string, string>(
                                        Uri.UnescapeDataString(parts[0]),
                                        Uri.UnescapeDataString(parts[1])
                                    ))
                            );
                            req.Headers.TryAddWithoutValidation("Accept", "text/plain");
                        }
                        else
                        {
                            req.Content = new StringContent(
                                def.RequestBody,
                                Encoding.UTF8,
                                "application/json"
                            );
                        }
                    }

                    if (def.PassTo == "Header" && !string.IsNullOrWhiteSpace(def.PassKey) && !string.IsNullOrWhiteSpace(def.RequestBody))
                    {
                        if (def.PassKey == "Authorization")
                            req.Headers.TryAddWithoutValidation(def.PassKey, $"Bearer {def.RequestBody}");
                        else
                            req.Headers.TryAddWithoutValidation(def.PassKey, def.RequestBody);
                    }

                    var response = await http.SendAsync(req, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                        responseText = Encoding.UTF8.GetString(bytes);

                        Console.WriteLine($"HTML uzunlugu: {responseText.Length}");
                        Console.WriteLine($"ResponseArea: {def.ResponseArea}");

                        var trimmed = responseText.TrimStart();

                        if (def.ParseType?.ToUpper() == "JSON")
                        {
                            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                            {
                                isSuccess = true;
                                break;
                            }
                        }
                        else
                        {
                            isSuccess = true;
                            break;
                        }
                    }

                    Console.WriteLine($"WEB WARNING → Deneme {attempt}/{maxTry} başarısız. Durum: {response.StatusCode}");
                    if (attempt < maxTry) await Task.Delay(2000, ct);
                }

                if (!isSuccess)
                {
                    await SendOperationAsync(def.Id, "WEB_CALL", def.Endpoint, "3 deneme sonunda başarısız", "FAILED", ct);
                    await ResetDefinition(def.Id, ct);
                    return;
                }

                if (string.IsNullOrWhiteSpace(responseText) || responseText.Length < 100)
                {
                    Console.WriteLine($"[DENEME]RPA deneniyor...{def.Name}");
                    responseText = await GetHtmlWithRpa(def.Endpoint, def);
                    Console.WriteLine($"[DENEME] RPA Metodu bitti. Uzunluk: {responseText?.Length}");
                }

                if (!isSuccess)
                {
                    await SendOperationAsync(def.Id, "WEB_CALL", def.Endpoint, "Başarısız", "FAILED", ct);
                    await ResetDefinition(def.Id, ct);
                    return;
                }
                List<Dictionary<string, object?>> rows;

                if (string.Equals(def.ParseType, "HTML", StringComparison.OrdinalIgnoreCase))
                {
                    rows = ParseHtmlRows(responseText, def.ResponseArea ?? "", def.ColumnMap);
                    if (rows == null || rows.Count == 0)
        rows = ParseHtmlRows(responseText, def.ResponseArea ?? "", def.ColumnMap);
                }
                else if (string.Equals(def.ParseType, "XML", StringComparison.OrdinalIgnoreCase))
                {
                    rows = ParseXmlRows(responseText, def.ResponseArea);
                }
                else if (string.Equals(def.ParseType, "XLSX", StringComparison.OrdinalIgnoreCase))
                {
                    rows = await ParseXlsxFromUrlAsync(def.Endpoint, def.ResponseArea, def.ColumnMap, ct);
                }

                else
                {
                    rows = ParseJsonRows(responseText);
                }

                Console.WriteLine($"[DEBUG] Rows sayısı: {rows?.Count}");
                Console.WriteLine($"[DEBUG] İlk row: {System.Text.Json.JsonSerializer.Serialize(rows?.FirstOrDefault())}");

                if (rows != null && rows.Count > 0)
                {
                    await PostDataToWebService(def.Id, def.TableName, rows, ct);
                    await SendOperationAsync(def.Id, "WEB_CALL", def.Endpoint, "Veri Başarıyla Çekildi", "SUCCESS", ct);
                    await MarkDefinitionComplete(def.Id, ct);
                    Console.WriteLine($"WEB OK → {def.TableName} ({rows.Count} satır eklendi)");
                }
                else
                {
                    throw new Exception("Çekilen veri boş veya uygun formatta değil.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WEB ERROR (DefId={def.Id}) → {ex.Message}");
            await SendOperationAsync(def.Id, "WEB_CALL", def.Endpoint, ex.Message, "ERROR", ct);
            await ResetDefinition(def.Id, ct);
        }
    }


    private readonly string _ozelIndirmeYolu = @"C:\Users\BOYRAZ\Documents\GitHub\web-service-agent\File";
    private List<Dictionary<string, object?>> _dosyadanGelenVeriler = new List<Dictionary<string, object?>>();

    private async Task<string> GetHtmlWithRpa(string url, dynamic definition)
    {
        PrepareDownloadDirectory();

        try
        {
            if (Directory.Exists(_ozelIndirmeYolu))
            {
                foreach (var file in Directory.GetFiles(_ozelIndirmeYolu))
                {
                    File.Delete(file);
                }
                Console.WriteLine("[RPA] İndirme klasörü temizlendi.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RPA] Klasör temizlenirken hata (dosya kullanımda olabilir): {ex.Message}");
        }

        Console.WriteLine($"[RPA] İşlem başlıyor: {url}");

        var options = new OpenQA.Selenium.Chrome.ChromeOptions();
        options.AddArgument("--no-sandbox");
        options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");

        options.AddUserProfilePreference("download.default_directory", _ozelIndirmeYolu);
        options.AddUserProfilePreference("download.prompt_for_download", false);

        try
        {
            using var driver = new OpenQA.Selenium.Chrome.ChromeDriver(options);
            driver.Navigate().GoToUrl(url);

            try
            {
                var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(15));
                wait.Until(d => d.FindElements(OpenQA.Selenium.By.XPath(
                    "//a[contains(@href,'.xlsx') or contains(@href,'.xls') or contains(@href,'.csv')]")).Count > 0);
                Console.WriteLine("[RPA] Dosya linkleri yüklendi.");
            }
            catch
            {
                Console.WriteLine("[RPA] 15sn beklendi ama linkler gelmedi, devam ediliyor.");
            }

            try
            {
                if (definition != null && !string.IsNullOrEmpty((string)definition.ResponseArea))
                {
                    var checkElement = driver.FindElement(OpenQA.Selenium.By.XPath(definition.ResponseArea));
                    if (checkElement != null && checkElement.Displayed)
                    {
                        Console.WriteLine($"[RPA] {definition.Name}: Veri ekranda bulundu, dosya indirmeye gerek yok.");
                        return driver.PageSource;
                    }
                }
            }
            catch
            {
                Console.WriteLine("[RPA] Hedef alan ekranda yok, indirme denenecek.");
            }


            try
            {
                var downloadBtn = driver.FindElement(OpenQA.Selenium.By.XPath("//button[contains(., 'Export')] | " +
                    "//a[contains(@class, 'export')] | //button[contains(., 'XLXSS')] | //button[contains(., 'CSV')]"));

                if (downloadBtn != null)
                {
                    Console.WriteLine("[RPA] İndirme butonu tespit edildi, tıklanıyor...");
                    ((OpenQA.Selenium.IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", downloadBtn);
                    await Task.Delay(5000);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("[RPA] Sitede otomatik bir indirme butonu bulunamadı, manuel devam ediliyor.");

                try
                {
                    var fileLinks = driver.FindElements(OpenQA.Selenium.By.XPath(
     "//a[contains(@href,'.xlsx') or contains(@href,'.xls') or contains(@href,'.csv') or contains(@href,'document_library/get_file') or contains(@href,'document_library')]"));

                    if (fileLinks.Count == 0)
                    {
                        Console.WriteLine("[RPA] Hiç dosya linki bulunamadı.");

                        var rssLinkler = driver.FindElements(OpenQA.Selenium.By.XPath(
                            "//a[contains(@href,'rss') or contains(text(),'RSS') or contains(@title,'RSS')]"));
                        Console.WriteLine($"[DEBUG] RSS link sayısı: {rssLinkler.Count}");
                        foreach (var r in rssLinkler)
                        {
                            Console.WriteLine($"  [RSS] {r.GetAttribute("href")}");
                        }

                        var tumLinkler = driver.FindElements(OpenQA.Selenium.By.TagName("a"));
                            Console.WriteLine($"[DEBUG] Sayfada toplam {tumLinkler.Count} link var:");
                            foreach (var link in tumLinkler.Take(20)) 
                            {
                                var href = link.GetAttribute("href") ?? "(href yok)";
                                var text = link.Text?.Trim() ?? "(metin yok)";
                                Console.WriteLine($"  -> [{text}] {href}");
                            }
                        }
                    
                    else
                    {
                        Console.WriteLine($"[RPA] {fileLinks.Count} adet dosya linki bulundu.");

                        var tableNames = ((string)definition.TableName).Contains(',')
     ? ((string)definition.TableName).Split(',').Select(t => t.Trim()).ToList()
     : new List<string> { (string)definition.TableName };

                        var orderedLinks = fileLinks
                            .OrderBy(l => {
                                var h = l.GetAttribute("href")?.ToLower() ?? "";
                                if (h.Contains(".xlsx")) return 0;
                                if (h.Contains(".xls")) return 1;
                                if (h.Contains(".csv")) return 2;
                                return 3;
                            }).ToList();

                        var cookies = driver.Manage().Cookies.AllCookies;
                        var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));

                        using var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        if (!string.IsNullOrEmpty(cookieHeader))
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);

                        if (orderedLinks.Count == 1 || tableNames.Count == 1)
                        {
                            string fileUrl = orderedLinks.First().GetAttribute("href");
                            if (!fileUrl.StartsWith("http"))
                            {
                                var baseUri = new Uri(driver.Url);
                                fileUrl = new Uri(baseUri, fileUrl).ToString();
                            }
                            var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                            string savePath = Path.Combine(_ozelIndirmeYolu, $"file_0.csv");
                            await File.WriteAllBytesAsync(savePath, fileBytes);
                        }
                        else
                        {
                            for (int idx = 0; idx < orderedLinks.Count && idx < tableNames.Count; idx++)
                            {
                                string fileUrl = orderedLinks[idx].GetAttribute("href");
                                if (!fileUrl.StartsWith("http"))
                                {
                                    var baseUri = new Uri(driver.Url);
                                    fileUrl = new Uri(baseUri, fileUrl).ToString();
                                }
                                Console.WriteLine($"[RPA] İndiriliyor ({idx + 1}/{orderedLinks.Count}): {fileUrl}");
                                var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                                string savePath = Path.Combine(_ozelIndirmeYolu, $"file_{idx}.csv");
                                await File.WriteAllBytesAsync(savePath, fileBytes);
                                Console.WriteLine($"[RPA] Kaydedildi: file_{idx}.csv → {tableNames[idx]}");
                            }
                        }

                        
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RPA] Dosya linki işlenirken hata: {ex.Message}");
                }
            }
        
        
            //inen dosyayı kontrol eden yer

            int denemeSayisi = 0;
            while (denemeSayisi < 20)
            {
                var dosyalar = Directory.GetFiles(_ozelIndirmeYolu)
               .Where(f => !f.EndsWith(".ini") && !f.EndsWith(".tmp") && !f.EndsWith(".crdownload"))
               .ToArray();


                if (dosyalar.Length > 0)
                {
                    var tableNameList = ((string)definition.TableName).Contains(',')
                        ? ((string)definition.TableName).Split(',').Select(t => t.Trim()).ToList()
                        : new List<string> { (string)definition.TableName };

                    if (dosyalar.Length == 1)
                    {
                        string yol = dosyalar[0];
                        Console.WriteLine($"[RPA] Dosya başarıyla indi: {Path.GetFileName(yol)}");
                        _dosyadanGelenVeriler = ReadDownloadedFile(yol, (string)definition.TableName);
                    }
                    else
                    {
                        var siraliDosyalar = dosyalar.OrderBy(f => f).ToList();
                        for (int idx = 0; idx < siraliDosyalar.Count && idx < tableNameList.Count; idx++)
                        {
                            string yol = siraliDosyalar[idx];
                            Console.WriteLine($"[RPA] Dosya okunuyor: {Path.GetFileName(yol)} → {tableNameList[idx]}");
                            var rows = ReadDownloadedFile(yol, tableNameList[idx]);
                            _multitables[tableNameList[idx]] = rows;
                        }
                    }
                    break;
                }

                await Task.Delay(1000);
                denemeSayisi++;
            }
            return driver.PageSource;
        }

        catch (Exception ex)
        {
            Console.WriteLine($"[RPA HATASI] {ex.Message}");
            return "";
        }
        return "";
    }



    //dosya okuma
    private Dictionary<string, List<Dictionary<string, object?>>> _multitables = new();
    private List<Dictionary<string, object?>> ReadDownloadedFile(string filePath, string tableName = "")
    {
        var data = new List<Dictionary<string, object?>>();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return data;

        string extension = Path.GetExtension(filePath).ToLower();

        if (extension == ".xlsx" || extension == ".xls")
        {
            data = ReadExcelFile(filePath);
        }
        else if (extension == ".csv")
        {
            if (tableName.Contains(','))
            {
                var tableNames = tableName.Split(',').Select(t => t.Trim()).ToList();
                var tableBlocks = ReadCsvFileMultiTable(filePath);

                for (int i = 0; i < tableNames.Count && i < tableBlocks.Count; i++)
                {
                    _multitables[tableNames[i]] = tableBlocks[i].Rows;
                    Console.WriteLine($"[CSV] {tableNames[i]} → {tableBlocks[i].Rows.Count} satır");
                }
                return tableBlocks.Count > 0 ? tableBlocks[0].Item2 : data;
            }
            else
            {
                var tableBlocks = ReadCsvFileMultiTable(filePath);
                data = tableBlocks.Count > 0 ? tableBlocks[0].Item2 : new List<Dictionary<string, object?>>();

            }
        }
        else if (extension == ".pdf")
        {
            Console.WriteLine("pdfkısmı.");
        }

        return data;
    }

    //excel okuma
    private List<Dictionary<string, object?>> ReadExcelFile(string filePath)
    {
        var result = new List<Dictionary<string, object?>>();

       
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
        {
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataset = reader.AsDataSet();
                if (dataset.Tables.Count > 0)
                {
                    var table = dataset.Tables[0]; 

                    for (int i = 1; i < table.Rows.Count; i++) 
                    {
                        var rowDict = new Dictionary<string, object?>();
                        for (int j = 0; j < table.Columns.Count; j++)
                        {
                            rowDict[$"Kolon{j + 1}"] = table.Rows[i][j]?.ToString()?.Trim();
                        }
                        result.Add(rowDict);
                    }
                }
            }
        }
        return result;
    }

    //csv
    private List<(string TableName, List<Dictionary<string, object?>> Rows)> ReadCsvFileMultiTable(string filePath)
    {
        var result = new List<(string, List<Dictionary<string, object?>>)>();


        var bytes = File.ReadAllBytes(filePath);
        var encoding = System.Text.Encoding.UTF8;

        // BOM kontrolü
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            encoding = System.Text.Encoding.UTF8;
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            encoding = System.Text.Encoding.Unicode;
        else
            encoding = System.Text.Encoding.GetEncoding("windows-1251");

        var allLines = System.Text.Encoding.UTF8.GetString(
            System.Text.Encoding.Convert(encoding, System.Text.Encoding.UTF8, bytes)
        ).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        var blocks = new List<List<string>>();
        var currentBlock = new List<string>();

        foreach (var line in allLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentBlock.Count > 0)
                {
                    blocks.Add(currentBlock);
                    currentBlock = new List<string>();
                }
            }
            else
            {
                currentBlock.Add(line);
            }
        }
        if (currentBlock.Count > 0)
            blocks.Add(currentBlock);

        var tableBlocks = blocks.Where(b => b.Count >= 2 && b.Any(l => l.Contains(';'))).ToList();


        return tableBlocks.Select(b =>
        {
            var rows = new List<Dictionary<string, object?>>();
            var firstRow = b[0].Split(';').Select(h => h.Trim().Trim('"')).ToList();
            bool isMergedHeader = firstRow.Count == 1 ||
                      firstRow.Count(h => string.IsNullOrWhiteSpace(h)) >= 1;

            List<string> headers;
            int dataStartIndex;

            if (isMergedHeader && b.Count >= 3)
            {
                headers = b[1].Split(';').Select(h => h.Trim().Trim('"')).ToList();
                dataStartIndex = 2;
            }
            else
            {
                headers = firstRow;
                dataStartIndex = 1;
            }

            var finalHeaders = new List<string>();
            var seenHeaders = new HashSet<string>();
            foreach (var h in headers)
            {
                var uniqueH = h;
                int count = 1;
                while (seenHeaders.Contains(uniqueH))
                {
                    count++;
                    uniqueH = $"{h}_{count}";
                }
                seenHeaders.Add(uniqueH);
                finalHeaders.Add(uniqueH);
            }
            headers = finalHeaders;

            for (int i = dataStartIndex; i < b.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(b[i])) continue;
                var values = b[i].Split(';');
                var row = new Dictionary<string, object?>();
                for (int j = 0; j < headers.Count && j < values.Length; j++)
                    row[headers[j]] = values[j].Trim().Trim('"');
                rows.Add(row);
            }

            return ("", rows); 
        }).ToList();
    }


    private void PrepareDownloadDirectory()
    {
        if (!Directory.Exists(_ozelIndirmeYolu))
        {
            Directory.CreateDirectory(_ozelIndirmeYolu);
            Console.WriteLine($"[SİSTEM] Klasör oluşturuldu: {_ozelIndirmeYolu}");
        }
        else
        {
            
            Console.WriteLine("[SİSTEM] Klasör hazır.");
        }
    }

    private async Task CallRestService(Definition def, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        try
        {
            Console.WriteLine("REST çağrılıyor...");

            await MarkDefinitionStart(def.Id, ct);
            Console.WriteLine(">>> START verildi. DB'den IsProcessing kontrol et. Devam için 12 sn bekleniyor...");
            await Task.Delay(12500, ct);

            var responseText = await http.GetStringAsync(def.Endpoint, ct);

            Console.WriteLine($"{def.TableName} verisi WebService'e gönderiliyor...");

            List<Dictionary<string, object?>> rows;
            if (string.Equals(def.ParseType, "XML", StringComparison.OrdinalIgnoreCase))
            {
                rows = ParseXmlRows(responseText, def.ResponseArea);
            }
            else
            {
                rows = ParseJsonRows(responseText);
            }
            await PostDataToWebService(def.Id, def.TableName, rows, ct);

            Console.WriteLine($"REST OK → {def.TableName} yazıldı (DefId={def.Id})");

            await SendOperationAsync(
                def.Id,
                "REST_CALL",
                def.Endpoint,
                responseText,
                "SUCCESS",
                ct
            );

            await MarkDefinitionComplete(def.Id, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"REST ERROR (DefId={def.Id}) → {ex.Message}");
            await ResetDefinition(def.Id, ct);
        }
    }

    private async Task CallSoapService(Definition def, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        var soapAction = $"http://tempuri.org/{def.MethodName}";

        var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <{def.MethodName} xmlns=""http://tempuri.org/"" />
  </soap:Body>
</soap:Envelope>";

        try
        {
            Console.WriteLine("SOAP çağrılıyor...");

            await MarkDefinitionStart(def.Id, ct);
            Console.WriteLine(">>> START verildi. DB'den IsProcessing kontrol et. Devam için 12 sn bekleniyor...");
            await Task.Delay(12500, ct);

            using var req = new HttpRequestMessage(HttpMethod.Post, def.Endpoint);
            req.Headers.Add("SOAPAction", soapAction);
            req.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");

            var resp = await http.SendAsync(req, ct);
            var xml = await resp.Content.ReadAsStringAsync(ct);

            Console.WriteLine($"{def.TableName} verisi WebService'e gönderiliyor...");
            Console.WriteLine("SOAP XML alındı.");

            var rows = ParseXmlRows(xml);
            Console.WriteLine($"SOAP parse bitti. Satır sayısı = {rows.Count}");

            await PostDataToWebService(def.Id, def.TableName, rows, ct);
            Console.WriteLine("SOAP data POST bitti.");

            Console.WriteLine($"SOAP OK → {def.TableName} yazıldı (DefId={def.Id})");
            await MarkDefinitionComplete(def.Id, ct);
            await SendOperationAsync(
            def.Id,
            "SOAP_CALL",
            def.Endpoint,
            xml,
            "SUCCESS",
            ct
);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SOAP ERROR (DefId={def.Id})");
            await ResetDefinition(def.Id, ct);
        }
    }

    private static List<Dictionary<string, object?>> ParseJsonRows(string json)
    {
        var result = new List<Dictionary<string, object?>>();

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var row = new Dictionary<string, object?>();

                foreach (var col in item.EnumerateObject())
                {
                    if (col.Name.Equals("email", StringComparison.OrdinalIgnoreCase))
                        continue;

                    row[col.Name] = GetJsonValue(col.Value);
                }

                result.Add(row);
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var row = new Dictionary<string, object?>();

            foreach (var col in doc.RootElement.EnumerateObject())
            {
                if (col.Name.Equals("email", StringComparison.OrdinalIgnoreCase))
                    continue;

                row[col.Name] = GetJsonValue(col.Value);
            }

            result.Add(row);
        }

        return result;
    }

    private static object? GetJsonValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString();

            case JsonValueKind.Number:
                if (value.TryGetInt32(out var intValue))
                    return intValue;

                if (value.TryGetInt64(out var longValue))
                    return longValue;

                if (value.TryGetDecimal(out var decimalValue))
                    return decimalValue;

                return value.ToString();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return value.GetBoolean();

            case JsonValueKind.Null:
                return null;

            default:
                return value.ToString();
        }
    }

    private static List<Dictionary<string, object?>> ParseXmlRows(string xml, string? rowElement = null)
    {
        var result = new List<Dictionary<string, object?>>();
        var doc = XDocument.Parse(xml);

        if (!string.IsNullOrWhiteSpace(rowElement))
        {
            var parts = rowElement.Split('|');
            var rowName = parts[0].Trim();
            var groupName = parts.Length > 1 ? parts[1].Trim() : null;
            var skipName = parts.Length > 2 ? parts[2].Trim() : null;

            var rowNodes = doc.Descendants()
                .Where(x => x.Name.LocalName == rowName)
                .ToList();

            foreach (var rowNode in rowNodes)
            {
                var groups = !string.IsNullOrWhiteSpace(groupName)
                    ? rowNode.Descendants().Where(x => x.Name.LocalName == groupName).ToList()
                    : null;

                if (groups != null && groups.Count > 0)
                {
                    var parentInfo = new Dictionary<string, object?>();
                    foreach (var el in rowNode.Elements())
                    {
                        if (el.Name.LocalName == groupName) continue;
                        if (!el.Elements().Any())
                            parentInfo[el.Name.LocalName] = ParseXmlValue(el.Value);
                        else
                            foreach (var child in el.Descendants().Where(x => !x.Elements().Any()))
                                parentInfo[el.Name.LocalName + "_" + child.Name.LocalName] = ParseXmlValue(child.Value);
                    }

                    foreach (var group in groups)
                    {
                        var groupMeta = new Dictionary<string, object?>();
                        foreach (var el in group.Elements())
                        {
                            if (!string.IsNullOrWhiteSpace(skipName) && el.Name.LocalName == skipName) continue;
                            if (!el.Elements().Any())
                                groupMeta[el.Name.LocalName] = ParseXmlValue(el.Value);
                            else
                                foreach (var child in el.Descendants().Where(x => !x.Elements().Any()))
                                    groupMeta[el.Name.LocalName + "_" + child.Name.LocalName] = ParseXmlValue(child.Value);
                        }

                        var leafName = group.Elements()
                            .FirstOrDefault(x => x.Elements().Any() &&
                                (string.IsNullOrWhiteSpace(skipName) || x.Name.LocalName != skipName))
                            ?.Name.LocalName;

                        if (leafName == null) continue;

                        foreach (var leaf in group.Elements().Where(x => x.Name.LocalName == leafName))
                        {
                            var row = new Dictionary<string, object?>(parentInfo);
                            foreach (var m in groupMeta) row[m.Key] = m.Value;
                            foreach (var el in leaf.Elements())
                                row[el.Name.LocalName] = ParseXmlValue(el.Value);
                            result.Add(row);
                        }
                    }
                }
                else
                {
                    var row = new Dictionary<string, object?>();
                    foreach (var el in rowNode.Descendants().Where(x => !x.Elements().Any()))
                        row[el.Name.LocalName] = ParseXmlValue(el.Value);
                    if (row.Count > 0)
                        result.Add(row);
                }
            }
        }
        else
        {
            var candidateNodes = doc.Descendants()
                .Where(x => x.Elements().Any() && x.Elements().All(e => !e.Elements().Any()))
                .ToList();
            foreach (var node in candidateNodes)
            {
                var row = new Dictionary<string, object?>();
                foreach (var child in node.Elements())
                    row[child.Name.LocalName] = ParseXmlValue(child.Value);
                if (row.Count > 0)
                    result.Add(row);
            }
        }

        return result;
    }

    private static object? ParseXmlValue(string value)
    {
        if (int.TryParse(value, out var intValue))
            return intValue;

        if (decimal.TryParse(value, out var decimalValue))
            return decimalValue;

        if (bool.TryParse(value, out var boolValue))
            return boolValue;

        if (DateTime.TryParse(value, out var dateValue))
            return dateValue;

        return value;
    }

    private async Task PostDataToWebService(int definitionId, string tableName, List<Dictionary<string, object?>> rows, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        var payload = new
        {
            DefinitionId = definitionId,
            TableName = tableName,
            Rows = rows
        };

        var response = await http.PostAsJsonAsync("http://localhost:5000/api/data", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task MarkDefinitionComplete(int definitionId, CancellationToken ct)
    {
        await RetryAsync(async () =>
        {
            var http = _httpClientFactory.CreateClient();

            var url = $"http://localhost:5000/api/definitions/{definitionId}/complete";

            var response = await http.PutAsync(url, null, ct);

            response.EnsureSuccessStatusCode();
        },
        $"COMPLETE (DefId={definitionId})",
        ct);
    }
    private async Task MarkDefinitionStart(int definitionId, CancellationToken ct)
    {
        await RetryAsync(async () =>
        {
            var http = _httpClientFactory.CreateClient();

            var response = await http.PutAsync(
                $"http://localhost:5000/api/definitions/{definitionId}/start",
                null,
                ct);

            response.EnsureSuccessStatusCode();
        },
        $"START (DefId={definitionId})",
        ct);

        Console.WriteLine($"START OK → DefinitionId={definitionId}");
    }

    private async Task ResetDefinition(int definitionId, CancellationToken ct)
    {
        await RetryAsync(async () =>
        {
            var http = _httpClientFactory.CreateClient();

            var url = $"http://localhost:5000/api/definitions/{definitionId}/reset";

            var response = await http.PutAsync(url, null, ct);

            response.EnsureSuccessStatusCode();
        },
        $"RESET (DefId={definitionId})",
        ct);
    }

    private async Task SendOperationAsync(
    int definitionId,
    string operationName,
    string requestPayload,
    string responsePayload,
    string status,
    CancellationToken ct)
    {
        await RetryAsync(async () =>
        {
            var http = _httpClientFactory.CreateClient();

            var payload = new
            {
                DefinitionId = definitionId,
                OperationName = operationName,
                RequestPayload = requestPayload,
                ResponsePayload = responsePayload,
                Status = status
            };

            var response = await http.PostAsJsonAsync(
                "http://localhost:5000/api/operations",
                payload,
                ct
            );

            response.EnsureSuccessStatusCode();
        },
        $"OPERATION (DefId={definitionId})",
        ct);
    }


    private async Task RetryAsync(Func<Task> action, string operationName, CancellationToken ct)
    {
        int maxTry = 3;
        int delayMs = 500;

        for (int attempt = 1; attempt <= maxTry; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{operationName} ERROR → deneme {attempt}/{maxTry}: {ex.Message}");

                if (attempt == maxTry)
                {
                    throw;
                }

                await Task.Delay(delayMs, ct);
            }
        }
    }
    private async Task CallChainedService(List<Definition> defs, Definition childDef, CancellationToken ct)
    {
        Console.WriteLine($"CHAIN START → Child DefId={childDef.Id}, Main DefId={childDef.MainDefinitionId}");

        var allDefinitions = await GetAllDefinitions(ct);
        var chain = BuildDefinitionChain(allDefinitions, childDef);

        Console.WriteLine("CHAIN ORDER → " + string.Join(" -> ", chain.Select(x => x.Id)));

        string? carriedValue = null;

        for (int i = 0; i < chain.Count; i++)
        {
            var currentDef = chain[i];

            var responseText = await CallDefinitionAndGetValue(currentDef, carriedValue, ct);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                Console.WriteLine($"CHAIN ERROR → Response boş. DefId={currentDef.Id}");
                await ResetDefinition(childDef.Id, ct);
                return;
            }

            if (!string.IsNullOrWhiteSpace(currentDef.ResponseArea))
            {
                carriedValue = ExtractValueFromJson(responseText, currentDef.ResponseArea);
            }
            else
            {
                carriedValue = responseText.Trim();
            }

            if (i == chain.Count - 1)
            {
                await MarkDefinitionStart(currentDef.Id, ct);
                Console.WriteLine(">>> START verildi...");
                Thread.Sleep(15000);

                if (!string.IsNullOrWhiteSpace(currentDef.ResponseArea)
                    && currentDef.ResponseArea.Contains(',')
                    && !string.IsNullOrWhiteSpace(currentDef.TableName)
                    && currentDef.TableName.Contains(','))
                {
                    var tableNames = currentDef.TableName.Split(',').Select(t => t.Trim()).ToList();
                    var responseAreas = currentDef.ResponseArea.Split(',').Select(r => r.Trim()).ToList();

                    using var doc = JsonDocument.Parse(responseText);

                    for (int t = 0; t < tableNames.Count && t < responseAreas.Count; t++)
                    {
                        if (!doc.RootElement.TryGetProperty(responseAreas[t], out var element)) continue;

                        List<Dictionary<string, object?>> rows;

                        if (element.ValueKind == JsonValueKind.Array)
                        {
                            rows = ParseJsonRows(element.GetRawText());
                        }
                        else if (element.ValueKind == JsonValueKind.Object)
                        {
                            rows = new List<Dictionary<string, object?>>();
                            var row = new Dictionary<string, object?>();
                            foreach (var prop in element.EnumerateObject())
                                row[prop.Name] = GetJsonValue(prop.Value);
                            rows.Add(row);
                        }
                        else continue;

                        Console.WriteLine($"{tableNames[t]} verisi gönderiliyor → {rows.Count} satır");
                        await PostDataToWebService(currentDef.Id, tableNames[t], rows, ct);
                    }
                }
                else
                {
                    List<Dictionary<string, object?>> rows;
                    if (string.Equals(currentDef.ParseType, "XML", StringComparison.OrdinalIgnoreCase))
                    {
                        rows = ParseXmlRows(responseText, currentDef.ResponseArea);
                    }
                    else
                    {
                        rows = ParseJsonRows(responseText);
                    }
                    Console.WriteLine($"{currentDef.TableName} verisi WebService'e gönderiliyor...");
                    await PostDataToWebService(currentDef.Id, currentDef.TableName, rows, ct);
                }

                Console.WriteLine($"CHAIN FINAL OK ␦ {currentDef.TableName} yazıldı (DefId={currentDef.Id})");

                await SendOperationAsync(
                    currentDef.Id,
                    "CHAIN_CALL",
                    currentDef.Endpoint,
                    responseText,
                    "SUCCESS",
                    ct
                );

                await MarkDefinitionComplete(currentDef.Id, ct);
            }
        }
    }
    private async Task<string?> CallMainDefinitionAndGetValue(Definition mainDef, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        try
        {
            Console.WriteLine($"MAIN DEF çağrılıyor... DefId={mainDef.Id}");

            string responseText = "";

            if (mainDef.ServiceType == "REST")
            {
                if (mainDef.MethodName?.ToUpper() == "POST")
                {
                    var content = new StringContent(
                        mainDef.RequestBody ?? "{}",
                        Encoding.UTF8,
                        "application/json");

                    var response = await http.PostAsync(mainDef.Endpoint, content, ct);
                    response.EnsureSuccessStatusCode();

                    responseText = await response.Content.ReadAsStringAsync(ct);
                }
                else
                {
                    responseText = await http.GetStringAsync(mainDef.Endpoint, ct);
                }
            }
            else
            {
                Console.WriteLine($"MAIN DEF ERROR → Şimdilik sadece REST main definition destekleniyor. DefId={mainDef.Id}");
                return null;
            }

            Console.WriteLine($"MAIN DEF OK → DefId={mainDef.Id}");

            return ExtractValueFromJson(responseText, mainDef.ResponseArea);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MAIN DEF ERROR → DefId={mainDef.Id} | {ex.Message}");
            return null;
        }
    }

    private string? ExtractValueFromJson(string json, string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return null;

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty(fieldName, out var value))
        {
            return value.ToString();
        }

        return null;
    }

    private async Task CallRestServiceWithInjectedValue(Definition def, string injectedValue, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        try
        {
            Console.WriteLine($"CHAIN CHILD REST çağrılıyor... DefId={def.Id}");

            await MarkDefinitionStart(def.Id, ct);

            Console.WriteLine(">>> START verildi. DB'den IsProcessing kontrol et. Devam için 12 sn bekleniyor...");
            Thread.Sleep(12000);

            HttpMethod method = HttpMethod.Get;

            if (!string.IsNullOrWhiteSpace(def.MethodName))
            {
                method = def.MethodName.ToUpper() switch
                {
                    "POST" => HttpMethod.Post,
                    "GET" => HttpMethod.Get
                };
            }


            using var req = new HttpRequestMessage(method, def.Endpoint);

            if (method == HttpMethod.Post && !string.IsNullOrWhiteSpace(def.RequestBody))
            {
                if (def.ParseType?.ToUpper() == "FORM")
                {
                    var formPairs = new List<KeyValuePair<string, string>>();
                    foreach (var pair in def.RequestBody.Split('&'))
                    {
                        var eqIndex = pair.IndexOf('=');
                        if (eqIndex > 0)
                        {
                            var key = pair.Substring(0, eqIndex);
                            var value = pair.Substring(eqIndex + 1);
                            formPairs.Add(new KeyValuePair<string, string>(key, value));
                        }
                    }
                    req.Content = new FormUrlEncodedContent(formPairs);
                    if (!string.IsNullOrWhiteSpace(def.AcceptType))
                    {
                        req.Headers.TryAddWithoutValidation("Accept", def.AcceptType);
                    }

                }
                else
                {
                    req.Content = new StringContent(
                        def.RequestBody,
                        Encoding.UTF8,
                        "application/json"
                    );
                }
            }

            if (def.PassTo == "Header" && !string.IsNullOrWhiteSpace(def.PassKey))
            {
                if (def.PassKey == "Authorization")
                {
                    req.Headers.Add(def.PassKey, $"Bearer {injectedValue}");
                }
                else
                {
                    req.Headers.Add(def.PassKey, injectedValue);
                }
            }

            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var responseText = await resp.Content.ReadAsStringAsync(ct);

            Console.WriteLine($"{def.TableName} verisi WebService'e gönderiliyor...");

            var rows = ParseJsonRows(responseText);
            Console.WriteLine($"[DEBUG] Rows sayısı: {rows?.Count}");
            Console.WriteLine($"[DEBUG] İlk row: {System.Text.Json.JsonSerializer.Serialize(rows?.FirstOrDefault())}");

            await PostDataToWebService(def.Id, def.TableName, rows, ct);

            Console.WriteLine($"CHAIN CHILD REST OK ␦ {def.TableName} yazıldı (DefId={def.Id})");

            await SendOperationAsync(
                def.Id,
                "CHAIN_REST_CALL",
                def.Endpoint,
                responseText,
                "SUCCESS",
                ct
            );

            await MarkDefinitionComplete(def.Id, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CHAIN CHILD REST ERROR (DefId={def.Id}) → {ex.Message}");
            await ResetDefinition(def.Id, ct);
        }
    }

    private async Task<List<Definition>> GetAllDefinitions(CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        var defs = await http.GetFromJsonAsync<List<Definition>>(
            "http://localhost:5000/api/definitions",
            ct);

        return defs ?? new List<Definition>();
    }

    private List<Definition> BuildDefinitionChain(List<Definition> allDefinitions, Definition targetDef)
    {
        var chain = new List<Definition>();
        var current = targetDef;

        while (current != null)
        {
            chain.Insert(0, current);

            if (!current.MainDefinitionId.HasValue)
                break;

            current = allDefinitions.FirstOrDefault(x => x.Id == current.MainDefinitionId.Value);

            if (current == null)
                break;
        }

        return chain;
    }

    private async Task<string?> CallDefinitionAndGetValue(Definition def, string? injectedValue, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();

        try
        {
            Console.WriteLine($"CHAIN STEP çağrılıyor... DefId={def.Id}");

            string responseText = "";

            if (def.ServiceType == "REST")
            {
                HttpMethod method = HttpMethod.Get;

                if (!string.IsNullOrWhiteSpace(def.MethodName))
                {
                    method = def.MethodName.ToUpper() switch
                    {
                        "POST" => HttpMethod.Post,
                        "PUT" => HttpMethod.Put,
                        "DELETE" => HttpMethod.Delete,
                        _ => HttpMethod.Get
                    };
                }

                using var req = new HttpRequestMessage(method, def.Endpoint);

                Console.WriteLine($"[DEBUG] DefId={def.Id} | ParseType={def.ParseType} | Method={method} | Body={def.RequestBody}");

                if (method == HttpMethod.Post && !string.IsNullOrWhiteSpace(def.RequestBody))
                {
                    if (def.ParseType?.ToUpper() == "FORM")
                    {
                        var formPairs = new List<KeyValuePair<string, string>>();
                        foreach (var pair in def.RequestBody.Split('&'))
                        {
                            var eqIndex = pair.IndexOf('=');
                            if (eqIndex > 0)
                            {
                                var key = pair.Substring(0, eqIndex);
                                var value = pair.Substring(eqIndex + 1);
                                formPairs.Add(new KeyValuePair<string, string>(key, value));
                            }
                        }
                        req.Content = new FormUrlEncodedContent(formPairs);
                        req.Headers.TryAddWithoutValidation("Accept", "text/plain");
                    }
                    else
                    {
                        req.Content = new StringContent(
                            def.RequestBody,
                            Encoding.UTF8,
                            "application/json"
                        );
                    }
                }

                if (!string.IsNullOrWhiteSpace(injectedValue)
                    && def.PassTo == "Header"
                    && !string.IsNullOrWhiteSpace(def.PassKey))
                {
                    if (def.PassKey == "Authorization")
                    {
                        req.Headers.Add(def.PassKey, $"Bearer {injectedValue}");
                    }
                    else
                    {
                        req.Headers.Add(def.PassKey, injectedValue);
                    }
                }

                var response = await http.SendAsync(req, ct);
                response.EnsureSuccessStatusCode();

                responseText = await response.Content.ReadAsStringAsync(ct);
            }
            else
            {
                Console.WriteLine($"CHAIN STEP ERROR → Şimdilik sadece REST destekleniyor. DefId={def.Id}");
                return null;
            }


            return responseText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CHAIN STEP ERROR → DefId={def.Id} | {ex.Message}");
            return null;
        }
    }


    private static List<Dictionary<string, object?>> ParseHtmlRows(
    string html,
    string responseArea,
    string? columnMapping)
    {
        var result = new List<Dictionary<string, object?>>();
        var htmlDoc = new HtmlAgilityPack.HtmlDocument();
        htmlDoc.LoadHtml(html);

        var targetLink = htmlDoc.DocumentNode
            .SelectSingleNode($"//ul/li/h2/a[contains(@href,'{responseArea}')]");

        if (targetLink == null)
            return result;

        var parentUl = targetLink.Ancestors("ul").FirstOrDefault();
        if (parentUl == null)
            return result;

        var liItems = parentUl.SelectNodes("li[not(ul)]")
            ?.Select(x => x.InnerText.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (liItems == null || liItems.Count == 0)
            return result;

        var columns = string.IsNullOrWhiteSpace(columnMapping)
            ? liItems.Select((_, i) => $"Kolon{i + 1}").ToList()
            : columnMapping.Split(',').Select(x => x.Trim()).ToList();

        var row = new Dictionary<string, object?>();
        int liIndex = 0;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i] == "{DATE}")
            {
                row["Tarih"] = DateTime.Now.ToString("dd.MM.yyyy");
                continue; 
            }

            if (liIndex >= liItems.Count) break;

            if (columns[i].ToLower() == "skip")
            {
                liIndex++;
                continue;
            }

            row[columns[i]] = liItems[liIndex];
            liIndex++;
        }

        result.Add(row);
        return result;
    }

    private async Task<List<Dictionary<string, object?>>> ParseXlsxFromUrlAsync(
    string endpoint,
    string? responseArea,
    string? columnMap,
    CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/123.0.0.0 Safari/537.36");

        byte[] xlsxBytes;

        bool isDirectXlsx = endpoint.Contains(".xlsx", StringComparison.OrdinalIgnoreCase)
                         || endpoint.Contains(".xls", StringComparison.OrdinalIgnoreCase);

        if (isDirectXlsx)
        {
            Console.WriteLine($"XLSX direkt indiriliyor → {endpoint}");
            xlsxBytes = await http.GetByteArrayAsync(endpoint, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(responseArea))
                throw new Exception("HTML sayfadan XLSX çekmek için ResponseArea (aranacak keyword) zorunlu.");

            Console.WriteLine($"HTML sayfa taranıyor → {endpoint}");
            string html = await http.GetStringAsync(endpoint, ct);

            string fileUrl = FindXlsxLinkInHtml(html, endpoint, responseArea);
            Console.WriteLine($"XLSX linki bulundu → {fileUrl}");

            xlsxBytes = await http.GetByteArrayAsync(fileUrl, ct);
        }

        return ParseXlsxRows(xlsxBytes, columnMap);
    }

    private static string FindXlsxLinkInHtml(string html, string baseUrl, string keyword)
    {
        var matches = Regex.Matches(
            html,
            $@"href=""([^""]*{Regex.Escape(keyword)}[^""]*\.xlsx[^""]*)""|href=""([^""]+\.xlsx[^""]*)""\s*[^>]*>\s*[^<]*{Regex.Escape(keyword)}",
            RegexOptions.IgnoreCase
        );

        var allXlsx = Regex.Matches(html, @"href=""([^""]+\.xlsx[^""]*?)""", RegexOptions.IgnoreCase);
        Console.WriteLine($"Sayfada bulunan tüm .xlsx linkleri ({allXlsx.Count} adet):");
        foreach (Match m in allXlsx)
            Console.WriteLine($"  → {m.Groups[1].Value}");

        if (matches.Count == 0)
            throw new Exception($"Sayfada '{keyword}' içeren .xlsx linki bulunamadı.");

        string rawUrl = matches[0].Groups[1].Success
            ? matches[0].Groups[1].Value
            : matches[0].Groups[2].Value;

        rawUrl = rawUrl.Replace("&amp;", "&");

        if (rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return rawUrl;

        var baseUri = new Uri(baseUrl);
        return new Uri(baseUri, rawUrl).ToString();
    }

    private static List<Dictionary<string, object?>> ParseXlsxRows(
    byte[] xlsxBytes,
    string? columnMap)
    {
        var result = new List<Dictionary<string, object?>>();

        using var stream = new MemoryStream(xlsxBytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);

        var sheet = workbook.Worksheet(1);

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        int lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastRow < 2) return result;

        List<string> headers;

        if (!string.IsNullOrWhiteSpace(columnMap))
        {
            headers = columnMap.Split(',').Select(x => x.Trim()).ToList();
        }
        else
        {
            headers = new List<string>();
            for (int col = 1; col <= lastCol; col++)
            {
                var headerVal = sheet.Cell(1, col).GetValue<string>().Trim();
                headers.Add(string.IsNullOrWhiteSpace(headerVal) ? $"Kolon{col}" : headerVal);
            }
        }

        for (int row = 2; row <= lastRow; row++)
        {
            var rowData = new Dictionary<string, object?>();
            bool hasData = false;

            for (int col = 1; col <= headers.Count && col <= lastCol; col++)
            {
                string colName = headers[col - 1];

                if (colName.ToLower() == "skip") continue;

                if (colName == "{DATE}")
                {
                    rowData["Tarih"] = DateTime.Now.ToString("dd.MM.yyyy");
                    continue;
                }

                var cell = sheet.Cell(row, col);
                if (cell.IsEmpty()) continue;

                rowData[colName] = GetXlsxCellValue(cell);
                hasData = true;
            }

            if (hasData)
                result.Add(rowData);
        }

        return result;
    }

    private static object? GetXlsxCellValue(ClosedXML.Excel.IXLCell cell)
    {
        return cell.DataType switch
        {
            ClosedXML.Excel.XLDataType.Number => cell.GetValue<decimal>(),
            ClosedXML.Excel.XLDataType.Boolean => cell.GetValue<bool>(),
            ClosedXML.Excel.XLDataType.DateTime => cell.GetValue<DateTime>(),
            ClosedXML.Excel.XLDataType.Text => cell.GetValue<string>(),
            _ => cell.GetValue<string>()
        };
    }


    private static List<Dictionary<string, object?>> ParseHtmlTableRows(
    string html,
    string responseArea,
    string? columnMap)
    {
        var result = new List<Dictionary<string, object?>>();
        var htmlDoc = new HtmlAgilityPack.HtmlDocument();
        htmlDoc.LoadHtml(html);

        int tableIndex = 0;
        string className = responseArea;

        var match = System.Text.RegularExpressions.Regex.Match(responseArea, @"^(.+)\[(\d+)\]$");
        if (match.Success)
        {
            className = match.Groups[1].Value.Trim();
            tableIndex = int.Parse(match.Groups[2].Value);
        }

        var divNodes = htmlDoc.DocumentNode.SelectNodes($"//div[contains(@class,'{className}')]");
        if (divNodes == null || tableIndex >= divNodes.Count) return result;

        var targetDiv = divNodes[tableIndex];
        var table = targetDiv.SelectSingleNode(".//table");
        if (table == null) return result;

        var rows = table.SelectNodes(".//tr");
        if (rows == null || rows.Count < 2) return result;

        var headerCells = rows[0].SelectNodes(".//th | .//td");
        if (headerCells == null) return result;

        var headers = string.IsNullOrWhiteSpace(columnMap)
            ? headerCells.Select((th, i) =>
                string.IsNullOrWhiteSpace(th.InnerText.Trim()) ? $"Kolon{i + 1}" : th.InnerText.Trim())
                .ToList()
            : columnMap.Split(',').Select(x => x.Trim()).ToList();

        for (int r = 1; r < rows.Count; r++)
        {
            var cells = rows[r].SelectNodes(".//td");
            if (cells == null) continue;

            var row = new Dictionary<string, object?>();
            for (int c = 0; c < cells.Count && c < headers.Count; c++)
            {
                row[headers[c]] = cells[c].InnerText.Trim();
            }
            if (row.Count > 0)
                result.Add(row);
        }

        return result;
    }



}