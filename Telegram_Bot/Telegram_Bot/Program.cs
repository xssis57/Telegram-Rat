using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;

namespace Telegram_Bot
{
    class Program
    {

       
        static TelegramBotClient bot;
        static long allowedUserId;

        static CancellationTokenSource cts;
        static NotifyIcon trayIcon;
        static ContextMenuStrip trayMenu;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        [STAThread]
        static void Main()
        {
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                .Build(); string botToken = configuration["General:Bot"]; 
            allowedUserId = configuration.GetValue<long>("General:Id"); 
            bot = new TelegramBotClient(botToken); 
            Application.EnableVisualStyles(); 
            Application.SetCompatibleTextRenderingDefault(false); 
            Task.Run(() => 
            StartBot()); 
            CreateTrayIcon(); 
            Application.Run();
        }

        static async Task StartBot()
        {
            cts = new CancellationTokenSource();

            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                cancellationToken: cts.Token
            );


            await Task.Delay(2000);


            try
            {
                string info = await GetPcInfo();

                await bot.SendMessage(
                    chatId: allowedUserId,
                    text: info
                );


                ShowTrayNotification("Bot Started", "Telegram Bot is running");
            }
            catch (Exception ex)
            {

            }
        }

        static void CreateTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show Status", null, OnShowStatus);
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon
            {
                Text = "Telegram Bot",
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += OnShowStatus;
        }

        static void OnShowStatus(object sender, EventArgs e)
        {
            MessageBox.Show("Telegram Bot is running in the background\n\nCommands:\n/info - PC Info\n/screenshot - Take screenshot\n/shutdown - Shutdown PC\n/restart - Restart PC\n/wallpaper - Change wallpaper\n/help - Show help",
                           "Bot Status",
                           MessageBoxButtons.OK,
                           MessageBoxIcon.Information);
        }

        static void OnExit(object sender, EventArgs e)
        {
            cts?.Cancel();
            trayIcon.Visible = false;
            Application.Exit();
        }

        static void ShowTrayNotification(string title, string text)
        {
            trayIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
        }

        static async Task HandleUpdateAsync(
            ITelegramBotClient botClient,
            Update update,
            CancellationToken ct)
        {
            if (update.Type != UpdateType.Message || update.Message?.Text == null)
                return;

            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;

            if (chatId != allowedUserId)
                return;


            ShowTrayNotification("Command Received", $"Command: {messageText}");

            if (messageText == "/info")
            {
                string info = await GetPcInfo();

                await botClient.SendMessage(
                    chatId: chatId,
                    text: info,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
            }
            else if (messageText == "/screenshot" || messageText.StartsWith("/screenshot "))
            {
                await HandleScreenshotCommand(botClient, chatId, messageText, ct);
            }
            else if (messageText == "/shutdown")
            {
                await HandleShutdownCommand(botClient, chatId, ct);
            }
            else if (messageText == "/restart")
            {
                await HandleRestartCommand(botClient, chatId, ct);
            }
            else if (messageText == "/wallpaper" || messageText.StartsWith("/wallpaper "))
            {
                await HandleWallpaperCommand(botClient, update, chatId, messageText, ct);
            }
            else if (messageText == "/help" || messageText == "/start")
            {
                await ShowHelp(botClient, chatId, ct);
            }
        }

        static async Task HandleScreenshotCommand(
            ITelegramBotClient botClient,
            long chatId,
            string messageText,
            CancellationToken ct)
        {
            try
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "📸 Taking screenshot, please wait...",
                    cancellationToken: ct
                );


                int width = 1920;
                int height = 1080;

                string[] parts = messageText.Split(' ');
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[1], out width);
                    int.TryParse(parts[2], out height);
                }

                string screenshotPath = await TakeScreenshot(width, height);

                using (var stream = System.IO.File.OpenRead(screenshotPath))
                {
                    var file = new InputFileStream(stream, "screenshot.png");
                    await botClient.SendPhoto(
                        chatId: chatId,
                        photo: file,
                        caption: $"📸 Screenshot ({width}x{height})",
                        cancellationToken: ct
                    );
                }


                System.IO.File.Delete(screenshotPath);

                ShowTrayNotification("Screenshot Sent", "Screenshot was sent successfully");
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ Failed to take screenshot: {ex.Message}",
                    cancellationToken: ct
                );
            }
        }

        static async Task HandleShutdownCommand(
            ITelegramBotClient botClient,
            long chatId,
            CancellationToken ct)
        {
            try
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Shutting down computer",
                    cancellationToken: ct
                );

                Process.Start("shutdown", "/s /t 0 /f");
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ Failed to shutdown: {ex.Message}",
                    cancellationToken: ct
                );
            }
        }

        static async Task HandleRestartCommand(
            ITelegramBotClient botClient,
            long chatId,
            CancellationToken ct)
        {
            try
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ Restarting computer",
                    cancellationToken: ct
                );

                Process.Start("shutdown", "/r /t 0 /f");
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ Failed to restart: {ex.Message}",
                    cancellationToken: ct
                );
            }
        }

        static async Task HandleWallpaperCommand(
            ITelegramBotClient botClient,
            Update update,
            long chatId,
            string messageText,
            CancellationToken ct)
        {
            try
            {

                if (update.Message.ReplyToMessage?.Type == MessageType.Photo)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "🖼️ Downloading image and setting as wallpaper...",
                        cancellationToken: ct
                    );


                    var photo = update.Message.ReplyToMessage.Photo.LastOrDefault();
                    if (photo != null)
                    {
                        string wallpaperPath = Path.Combine(Path.GetTempPath(), "telegram_wallpaper.jpg");


                        var fileInfo = await botClient.GetFile(photo.FileId, ct);

                        using (var stream = new FileStream(wallpaperPath, FileMode.Create))
                        {
                            await botClient.DownloadFile(fileInfo.FilePath, stream, ct);
                        }


                        bool result = SetWallpaper(wallpaperPath);

                        if (result)
                        {
                            await botClient.SendMessage(
                                chatId: chatId,
                                text: "✅ Wallpaper changed successfully!",
                                cancellationToken: ct
                            );
                        }
                        else
                        {
                            await botClient.SendMessage(
                                chatId: chatId,
                                text: "❌ Failed to set wallpaper",
                                cancellationToken: ct
                            );
                        }
                    }
                }
                else
                {

                    string[] parts = messageText.Split(' ');
                    if (parts.Length >= 2)
                    {
                        string url = parts[1];
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"🖼️ Downloading image from URL and setting as wallpaper...",
                            cancellationToken: ct
                        );

                        string wallpaperPath = Path.Combine(Path.GetTempPath(), "telegram_wallpaper.jpg");


                        using (HttpClient client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(30);
                            var imageBytes = await client.GetByteArrayAsync(url);
                            await File.WriteAllBytesAsync(wallpaperPath, imageBytes);
                        }


                        bool result = SetWallpaper(wallpaperPath);

                        if (result)
                        {
                            await botClient.SendMessage(
                                chatId: chatId,
                                text: "✅ Wallpaper changed successfully!",
                                cancellationToken: ct
                            );
                        }
                        else
                        {
                            await botClient.SendMessage(
                                chatId: chatId,
                                text: "❌ Failed to set wallpaper",
                                cancellationToken: ct
                            );
                        }
                    }
                    else
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: "❌ Please reply to a photo or provide an image URL. Usage:\n" +
                                  "• Reply to a photo with /wallpaper\n" +
                                  "• /wallpaper https://example.com/image.jpg",
                            cancellationToken: ct
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ Failed to change wallpaper: {ex.Message}",
                    cancellationToken: ct
                );
            }
        }

        static async Task ShowHelp(
            ITelegramBotClient botClient,
            long chatId,
            CancellationToken ct)
        {
            string helpText = @"🤖 *Available Commands:*

📸 */screenshot* - Take screenshot
   */screenshot 1920 1080* - Custom size

🖼️ */wallpaper* - Change wallpaper
   • Reply to a photo with /wallpaper
   • /wallpaper [image_url]

⚠️ */shutdown* - Shutdown PC
🔄 */restart* - Restart PC 


ℹ️ */info* - Get PC information
❓ */help* - Show this messages";

            await botClient.SendMessage(
                chatId: chatId,
                text: helpText,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct
            );
        }

        static Task HandleErrorAsync(
            ITelegramBotClient botClient,
            Exception exception,
            CancellationToken ct)
        {
            Debug.WriteLine($"Error: {exception.Message}");
            return Task.CompletedTask;
        }

        static async Task<string> GetPcInfo()
        {
            string publicIp = await GetPublicIp();
            string localIp = GetLocalIp();
            string osInfo = GetOperatingSystemInfo();
            string uptime = GetSystemUptime();

            return
    $@"🖥 *PC Information*
👤 User: {Environment.UserName}
💻 Machine: {Environment.MachineName}
🧠 OS: {osInfo}
⏰ Uptime: {uptime}
🌐 Public IP: {publicIp}
🏠 Local IP: {localIp}";
        }

        static string GetOperatingSystemInfo()
        {
            try
            {
                return Environment.OSVersion.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        static string GetSystemUptime()
        {
            try
            {
                TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
            }
            catch
            {
                return "Unknown";
            }
        }

        static async Task<string> GetPublicIp()
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                return await client.GetStringAsync("https://api.ipify.org");
            }
            catch
            {
                try
                {
                    using HttpClient client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    return await client.GetStringAsync("https://icanhazip.com");
                }
                catch
                {
                    return "Unavailable";
                }
            }
        }

        static string GetLocalIp()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch { }
            return "Unavailable";
        }

        static async Task<string> TakeScreenshot(int width = 1920, int height = 1080)
        {
            string tempFile = Path.GetTempFileName() + ".png";

            try
            {
                await Task.Run(() =>
                {

                    Rectangle bounds;

                    if (width <= 0 || height <= 0)
                    {
                        bounds = Rectangle.FromLTRB(0, 0, 1000, 900);
                    }
                    else
                    {
                        bounds = Rectangle.FromLTRB(0, 0, width, height);
                    }

                    using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (Graphics graphics = Graphics.FromImage(bmp))
                        {
                            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                        }
                        bmp.Save(tempFile, ImageFormat.Png);
                    }
                });


                if (!System.IO.File.Exists(tempFile) || new FileInfo(tempFile).Length == 0)
                {
                    throw new Exception("Screenshot file is empty or not created");
                }

                return tempFile;
            }
            catch
            {
                if (System.IO.File.Exists(tempFile))
                    System.IO.File.Delete(tempFile);
                throw;
            }
        }

        static bool SetWallpaper(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return false;


                string wallpaperDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "TelegramWallpapers");
                Directory.CreateDirectory(wallpaperDir);

                string destPath = Path.Combine(wallpaperDir, $"wallpaper_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                File.Copy(imagePath, destPath, true);


                return SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, destPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE) != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}