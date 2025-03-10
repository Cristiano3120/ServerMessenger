namespace Server_Messenger
{
    public readonly record struct LoginRequest
    {
        public string Email { get; init; }

        public string Password { get; init; }

        public string Token { get; init; }

        public bool StayLoggedIn { get; init; }

        public bool IsEmpty()
            => string.IsNullOrEmpty(Email) && string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(Token);

        public void Deconstruct(out string email, out string password, out bool stayLoggedIn)
        {
            email = Email;
            password = Password;
            stayLoggedIn = StayLoggedIn;
        }
    }
}
