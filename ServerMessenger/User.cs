using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerMessenger
{
    /// <summary>
    /// Represents/saves user data.
    /// Implements IEnumerable(string)
    /// </summary>
    internal class User : IEnumerable<string>
    {
        public required string Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int Day { get; set; } = 0;
        public int Month { get; set; } = 0;
        public int Year { get; set; } = 0;

        public IEnumerator<string> GetEnumerator()
        {
            yield return Email;
            yield return Username;
            yield return Password;
            yield return FirstName;
            yield return LastName;
            yield return Day.ToString();
            yield return Month.ToString();
            yield return Year.ToString();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
