using BlazorDexie.Database;
using BlazorDexie.JsModule;
using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.client.CacheDB
{
    public class EgrooDB : Db
    {
        public Store<Message, Guid> Messages { get; set; } =
            new(
                nameof(Message.Id),
                nameof(Message.DateCreated),
                nameof(Message.DateUpdated),
                nameof(Message.DateDeleted),
                nameof(Message.SenderId),
                nameof(Message.ChannelId),
                nameof(Message.DateSent),
                nameof(Message.DateSeen),
                nameof(Message.DisplayName),
                nameof(Message.Content)
            );

        public EgrooDB(IModuleFactory moduleFactory)
            : base("EgrooDatabase", 1, new DbVersion[] { }, moduleFactory)
        {
        }
    }
}
