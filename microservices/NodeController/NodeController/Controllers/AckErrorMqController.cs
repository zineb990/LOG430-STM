﻿using ApplicationLogic.Interfaces;
using ApplicationLogic.Usecases;
using Entities.DomainInterfaces.ResourceManagement;
using MassTransit;
using MqContracts;

namespace NodeController.Controllers;

public class AckErrorMqController : IConsumer<AckErrorDto>
{
    private readonly Ingress _ingress;

    public AckErrorMqController(Ingress ingress)
    {
        _ingress = ingress;
    }

    public async Task Consume(ConsumeContext<AckErrorDto> context)
    {
       await _ingress.Register();
    }
}