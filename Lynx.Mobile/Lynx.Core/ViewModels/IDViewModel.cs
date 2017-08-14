﻿using System;
using System.Linq;
using System.Collections.Generic;
using Lynx.Core.Models.IDSubsystem;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using Attribute = Lynx.Core.Models.IDSubsystem.Attribute;
using MvvmCross.Core.Navigation;
using System.Threading.Tasks;

namespace Lynx.Core.ViewModels
{
    public class IDViewModel : MvxViewModel
    {
        public ID ID { get; set; }
        public List<Attribute> Attributes { get; set; }
        private IMvxNavigationService _navigationService;

        public IMvxCommand RequestVerificationCommand => new MvxCommand(RequestVerification);
        public IMvxCommand QrCodeScanCommand => new MvxCommand<string>(QrCodeScan);

        public IDViewModel(IMvxNavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        //TODO: For more information see: https://www.mvvmcross.com/documentation/fundamentals/navigation
        public void Init()
        {
            ID = Mvx.Resolve<ID>();
            Attributes = Mvx.Resolve<ID>().Attributes.Values.ToList();
        }

        public override void Start()
        {
            //TODO: Add starting logic here
        }

        private async void QrCodeScan(string content)
        {
            throw new NotImplementedException();
        }

        private async void RequestVerification()
        {
            await _navigationService.Navigate<VerificationRequestViewModel>();
        }
    }
}
