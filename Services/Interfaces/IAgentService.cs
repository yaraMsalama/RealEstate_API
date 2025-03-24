﻿using RealEstate.Services;
using RealEStateProject.Models;
using RealEStateProject.ViewModels.Agent;

namespace RealEStateProject.Services.Interfaces
{
    public interface IAgentService : IBaseService<Agent, AgentViewModel>
    {
        Task<Agent> GetAgentByUserIdAsync(string userId);

        Task<bool> IsUserAgentAsync(string userId);

        Task<AgentViewModel> GetAgentByPropertyIdAsync(int propertyId);

        Task<int> AddAgentAsync(Agent agent);

        Task UpdateAgentAsync(Agent agent);
    }
}
