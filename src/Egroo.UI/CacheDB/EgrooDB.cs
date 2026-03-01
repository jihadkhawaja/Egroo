using BlazorDexie.Database;
using BlazorDexie.Options;
using jihadkhawaja.chat.shared.Models;

namespace Egroo.UI.CacheDB
{
    public class EgrooDB : Db<EgrooDB>
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

        public EgrooDB(BlazorDexieOptions blazorDexieOptions)
            : base("EgrooDatabase", 1, new IDbVersion[] { }, blazorDexieOptions)
        {
        }
    }
}
