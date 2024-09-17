using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GiveawayBot
{
    public class Program
    {
        private static ITelegramBotClient botClient;
        private static ReceiverOptions receiverOptions;
        private static string token = "7006475150:AAF2_c7cH8qVUTguc-dsMv8OVJUqxNLsL4M";
        private static CancellationTokenSource cancellationTokenSource;
        private static HashSet<long> adminIds = new HashSet<long>() { 932635238 }; 
        private static List<Giveaway> giveaways = new List<Giveaway>();
        private static Dictionary<long, string> currentUserInputs = new Dictionary<long, string>(); 

        static async Task Main(string[] args)
        {
            botClient = new TelegramBotClient(token);
            receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                    UpdateType.Message,
                    UpdateType.CallbackQuery
                }
            };
            cancellationTokenSource = new CancellationTokenSource();

            botClient.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions, cancellationTokenSource.Token);
            Console.WriteLine("Бот запущен. Нажмите Enter, чтобы остановить...");
            Console.ReadLine();
        }

        private static async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            if (update.Type == UpdateType.Message && update.Message.Text != null)
            {
                var chatId = update.Message.Chat.Id;
                long userId = update.Message.From.Id;

                if (currentUserInputs.ContainsKey(userId))
                {
                    await ProcessUserInput(userId, update.Message.Text, chatId);
                    return;
                }

                switch (update.Message.Text)
                {
                    case "/start":
                        await ShowMainMenu(chatId);
                        break;

                    case "/show_giveaways":
                        await ShowGiveaways(chatId);
                        break;

                    case "/create_giveaway":
                        currentUserInputs[userId] = "Введите название розыгрыша:";
                        await botClient.SendTextMessageAsync(chatId, "Введите название розыгрыша:");
                        break;

                    case "/delete_giveaway":
                        currentUserInputs[userId] = "Введите индекс розыгрыша, который хотите удалить:";
                        await botClient.SendTextMessageAsync(chatId, "Введите индекс розыгрыша, который хотите удалить:");
                        break;

                    case "/grant_admin":
                        currentUserInputs[userId] = "Введите ID пользователя, которому хотите выдать права:";
                        await botClient.SendTextMessageAsync(chatId, "Введите ID пользователя, которому хотите выдать права:");
                        break;

                    default:
                        await botClient.SendTextMessageAsync(chatId, "Неизвестная команда. Пожалуйста, выберите из меню.");
                        break;
                }
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                var callbackQuery = update.CallbackQuery;
                var chatId = callbackQuery.Message.Chat.Id;

                await HandleCallbackQuery(client, callbackQuery);
            }
        }

        private static async Task ProcessUserInput(long userId, string input, long chatId)
        {
            if (!currentUserInputs.ContainsKey(userId))
                return;

            var action = currentUserInputs[userId];
            string title = string.Empty;
            string description = string.Empty;

            if (action == "Введите название розыгрыша:")
            {
                title = input;
                currentUserInputs[userId] = "Введите описание розыгрыша:";
                await botClient.SendTextMessageAsync(chatId, "Введите описание розыгрыша:");
                return;
            }
            else if (action == "Введите описание розыгрыша:")
            {
                description = input;
                currentUserInputs[userId] = "Введите URL изображения:";
                await botClient.SendTextMessageAsync(chatId, "Введите URL изображения:");
                return;
            }
            else if (action == "Введите URL изображения:")
            {
                var imageUrl = input;
                var newGiveaway = new Giveaway
                {
                    Title = currentUserInputs[userId],     
                    Description = description,              
                    ImageUrl = imageUrl
                };

                giveaways.Add(newGiveaway);
                await botClient.SendTextMessageAsync(chatId, "Розыгрыш успешно создан!");
                currentUserInputs.Remove(userId);
                return;
            }
            else if (action == "Введите индекс розыгрыша, который хотите удалить:")
            {
                if (int.TryParse(input, out int index) && index >= 0 && index < giveaways.Count)
                {
                    giveaways.RemoveAt(index);
                    await botClient.SendTextMessageAsync(chatId, "Розыгрыш успешно удалён!");
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Недопустимый индекс.");
                }
                currentUserInputs.Remove(userId);
                return;
            }
            else if (action == "Введите ID пользователя, которому хотите выдать права:")
            {
                if (long.TryParse(input, out long targetUserId))
                {
                    if (adminIds.Add(targetUserId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "Пользователь получил админские права!");
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Пользователь уже является администратором.");
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Неверный формат ID пользователя.");
                }
                currentUserInputs.Remove(userId);
                return;
            }
        }


        private static async Task ShowMainMenu(long chatId)
        {
            var buttons = new[]
            {
                InlineKeyboardButton.WithCallbackData("Создать розыгрыш", "create_giveaway"),
                InlineKeyboardButton.WithCallbackData("Удалить розыгрыш", "delete_giveaway"),
                InlineKeyboardButton.WithCallbackData("Выдать админские права", "grant_admin"),
                InlineKeyboardButton.WithCallbackData("Просмотреть розыгрыши", "show_giveaways")
            };

            var inlineKeyboard = new InlineKeyboardMarkup(new[] { buttons });
            await botClient.SendTextMessageAsync(chatId, "Главное меню:", replyMarkup: inlineKeyboard);
        }

        private static async Task HandleCallbackQuery(ITelegramBotClient client, CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            switch (callbackQuery.Data)
            {
                case "/show_giveaways":
                    await ShowGiveaways(chatId);
                    break;
                default:
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Неизвестная команда.");
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }

        private static async Task ShowGiveaways(long chatId)
        {
            if (giveaways.Count == 0)
            {
                await botClient.SendTextMessageAsync(chatId, "Нет доступных розыгрышей.");
                return;
            }

            var message = "Список розыгрышей:\n";
            for (int i = 0; i < giveaways.Count; i++)
            {
                var giveaway = giveaways[i];
                message += $"{i + 1}. {giveaway.Title}\n";
            }
            await botClient.SendTextMessageAsync(chatId, message);
        }

        private static Task ErrorHandler(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }

    public class Giveaway
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
    }
}
