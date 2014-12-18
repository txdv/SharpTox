using System;

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

        public string Name
        {
            get
            {
                return Tox.GetName(Number);
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

        public bool SetUserIsTyping(bool isTyping)
        {
            return Tox.SetUserIsTyping(Number, isTyping);
        }

        public bool GetIsTyping()
        {
            return Tox.GetIsTyping(Number);
        }

        public bool IsTyping
        {
            get
            {
                return GetIsTyping();
            }
            set
            {
                if (!SetUserIsTyping (value))
                {
                    throw new Exception ("Couldn't set isTyping for " + Number);
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

