using JihadKhawaja.SignalR.Server.Chat.Models;
using Microsoft.EntityFrameworkCore;
using MobileChat.Server.Database;
using MobileChat.Server.Interfaces;

namespace MobileChat.Server.Services
{
    public class MessageService : IMessage
    {
        private readonly DataContext context;
        public MessageService(DataContext context)
        {
            this.context = context;
        }
        public Task<bool> Create(Message entry)
        {
            try
            {
                context.Messages.Add(entry);
                context.SaveChanges();
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public Task<bool> Delete(Guid id)
        {
            try
            {
                Message entry = context.Messages.FirstOrDefault(x => x.Id == id);

                if (entry is not null)
                {
                    context.Messages.Remove(entry);
                    context.SaveChanges();

                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public Task<Message> ReadById(Guid id)
        {
            return Task.FromResult(context.Messages.FirstOrDefault(x => x.Id == id));
        }

        public Task<bool> Update(Message entry)
        {
            try
            {
                Message dbentry = context.Messages.FirstOrDefault(x => x.Id == entry.Id);

                if (dbentry is not null)
                {
                    context.Entry(dbentry).State = EntityState.Detached;
                    context.Messages.Update(entry);

                    context.SaveChanges();

                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return Task.FromResult(false);
            }
        }
    }
}
