﻿using Entities.DomainInterfaces.Live;
using Entities.DomainInterfaces.Planned;

namespace ApplicationLogic.Interfaces;

public interface IRepositoryWrite
{
    void AddOrUpdatePod(IPodInstance podInstance);
    
    void TryRemovePod(IPodInstance podInstance);

    void AddOrUpdatePodType(IPodType podType);

}