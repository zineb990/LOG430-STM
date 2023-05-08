﻿using IO.Swagger.Models;

namespace Entities.DomainInterfaces.Live;

public interface IContainerConfig
{
    public ContainerInspectResponse Config { get; set; }

    public string ContainerPort { get; set; }

}