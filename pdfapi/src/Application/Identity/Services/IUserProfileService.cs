using PaperAPI.Application.Identity.Responses;

namespace PaperAPI.Application.Identity.Services;

public interface IUserProfileService
{
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserProfileDto> GetProfileByEmailAsync(string email, CancellationToken cancellationToken);
    Task<UserAgreementsDto> GetAgreementsAsync(Guid userId, CancellationToken cancellationToken);
}
