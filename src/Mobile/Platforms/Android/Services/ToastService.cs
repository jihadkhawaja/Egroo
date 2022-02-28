using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using MobileChat.Droid.Services;
using MobileChat.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[assembly: Dependency(typeof(ToastService))]

namespace MobileChat.Droid.Services
{
    public class ToastService : IToast
    {
        public void Show(string message)
        {
            Android.Widget.Toast.MakeText(Android.App.Application.Context, message, ToastLength.Long).Show();
        }
    }
}