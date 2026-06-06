namespace Aries.Model
{
    public class UserConfig
    {
        public UserConfig()
        {
            Username = string.Empty;
            Password = string.Empty;
        }

        public string Username { get; set; }

        public string Password { get; set; }
    }
}
