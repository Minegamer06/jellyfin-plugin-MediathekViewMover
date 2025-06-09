using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Template.UserExamples
{
    /// <summary>
    /// Info Class.
    /// </summary>
    public class UserInfo
    {
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserInfo"/> class.
        /// </summary>
        /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
        public UserInfo(IUserManager userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Run Example.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        public async Task Run()
        {
            var userList = _userManager.Users;
            var user = userList.FirstOrDefault(u => u is not null);
            if (user is not null)
            {
                user.DisplayMissingEpisodes = true;
                if (user.LoginAttemptsBeforeLockout is null)
                {
                    user.LoginAttemptsBeforeLockout = 1;
                }
                else
                {
                    user.LoginAttemptsBeforeLockout++;
                }

                await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
            }
        }
    }
}
