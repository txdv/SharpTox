using System;
using System.Text;

namespace SharpTox.Core
{
    public class ToxFriend
    {
        public Tox Tox { get; private set; }
        public int Number { get; private set; }

        public ToxFriend(Tox tox, int friendNumber)
        {
            Tox = tox;
            Number = friendNumber;
        }

        /// <summary>
        /// Returns the name of a ToxFriend.
        /// </summary>
        /// <value>The name of a ToxFriend.</value>
        public string Name
        {
            get
            {
                Tox.CheckDisposed();

                int size = ToxFunctions.GetNameSize(Tox._tox, Number);
                byte[] name = new byte[size];

                ToxFunctions.GetName(Tox._tox, Number, name);

                return ToxTools.RemoveNull(Encoding.UTF8.GetString(name, 0, name.Length));
            }
        }

        public ToxUserStatus UserStatus
        {
            get
            {
                return Tox.GetUserStatus(Number);
            }
        }

        public DateTime LastOnline
        {
            get
            {
                return Tox.GetLastOnline(Number);
            }
        }

        public ToxFriendConnectionStatus OnlineStatus
        {
            get
            {
                return Tox.GetFriendConnectionStatus(Number);
            }
        }

        public int SendMessage(string message)
        {
            return Tox.SendMessage(Number, message);
        }

        public ToxKey GetClientId()
        {
            return Tox.GetClientId(Number);
        }

        public bool IsTyping
        {
            get
            {
                return Tox.GetIsTyping(Number);
            }
        }

        // TODO: Currently this is not save, since FriendFromFriendNumber
        // always returns a new instance
        private bool userIsTyping;
        public bool UserIsTyping
        {
            get
            {
                return userIsTyping;
            }
            set
            {
                if (!Tox.SetUserIsTyping(Number, value))
                {
                    throw new Exception("Couldn't set isTyping for " + Number);
                }
                else
                {
                    userIsTyping = value;
                }
            }
        }

        public bool SendAvatarInfo()
        {
            return Tox.SendAvatarInfo(Number);
        }

        public bool RequestAvatarInfo()
        {
            return Tox.RequestAvatarData(Number);
        }
    }
}
