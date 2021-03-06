﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SharpTox.Core;

namespace SharpTox.Av
{
    /// <summary>
    /// Represents an instance of toxav.
    /// </summary>
    public class ToxAv : IDisposable, IToxItertable
    {
        #region Event delegates
        private ToxAvDelegates.CallstateCallback _onCancelCallback;
        private ToxAvDelegates.CallstateCallback _onEndCallback;
        private ToxAvDelegates.CallstateCallback _onInviteCallback;
        private ToxAvDelegates.CallstateCallback _onPeerTimeoutCallback;
        private ToxAvDelegates.CallstateCallback _onRejectCallback;
        private ToxAvDelegates.CallstateCallback _onRequestTimeoutCallback;
        private ToxAvDelegates.CallstateCallback _onRingingCallback;
        private ToxAvDelegates.CallstateCallback _onStartCallback;
        private ToxAvDelegates.CallstateCallback _onPeerCSChangeCallback;
        private ToxAvDelegates.CallstateCallback _onSelfCSChangeCallback;
        private ToxAvDelegates.AudioReceiveCallback _onReceivedAudioCallback;
        private ToxAvDelegates.VideoReceiveCallback _onReceivedVideoCallback;
        #endregion

        private List<ToxAvDelegates.GroupAudioReceiveCallback> _groupAudioHandlers = new List<ToxAvDelegates.GroupAudioReceiveCallback>();
        private bool _disposed = false;
        private bool _running = false;
        private CancellationTokenSource _cancelTokenSource;

        /// <summary>
        /// The default codec settings.
        /// </summary>
        public static readonly ToxAvCodecSettings DefaultCodecSettings = new ToxAvCodecSettings()
        {
            CallType = ToxAvCallType.Audio,
            VideoBitrate = 500,
            MaxVideoWidth = 1200,
            MaxVideoHeight = 720,

            AudioBitrate = 64000,
            AudioFrameDuration = 20,
            AudioSampleRate = 48000,
            AudioChannels = 1
        };

        private ToxAvHandle _toxAv;

        /// <summary>
        /// The handle of this toxav instance.
        /// </summary>
        public ToxAvHandle Handle
        {
            get
            {
                return _toxAv;
            }
        }

        /// <summary>
        /// The tox instance.
        /// </summary>
        /// <value>The tox.</value>
        public Tox Tox { get; private set; }

        /// <summary>
        /// Retrieves the number of active calls.
        /// </summary>
        public int ActiveCalls
        {
            get
            {
                CheckDisposed();

                int count = ToxAvFunctions.GetActiveCount(_toxAv);
                return count == -1 ? 0 : count;
            }
        }

        /// <summary>
        /// The maximum amount of calls this instance of toxav is allowed to have.
        /// </summary>
        public int MaxCalls { get; private set; }

        /// <summary>
        /// Initialises a new instance of toxav.
        /// </summary>
        /// <param name="tox"></param>
        /// <param name="maxCalls"></param>
        internal ToxAv(Tox tox, int maxCalls)
        {
            Tox = tox;
            _toxAv = ToxAvFunctions.New(tox.Handle, maxCalls);

            if (_toxAv == null || _toxAv.IsInvalid)
                throw new Exception("Could not create a new instance of toxav.");

            MaxCalls = maxCalls;
        }

        /// <summary>
        /// Releases all resources used by this instance of tox.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        //dispose pattern as described on msdn for a class that uses a safe handle
        private void Dispose(bool disposing)
        {
            CheckDisposed();

            if (disposing)
            {
                if (_cancelTokenSource != null)
                {
                    _cancelTokenSource.Cancel();
                    _cancelTokenSource.Dispose();
                }
            }

            ClearEventSubscriptions();

            if (!_toxAv.IsInvalid && !_toxAv.IsClosed && _toxAv != null)
                _toxAv.Dispose();

            _disposed = true;
        }

        private void ClearEventSubscriptions()
        {
            _onCancel = null;
            _onEnd = null;
            _onInvite = null;
            _onPeerTimeout = null;
            _onReceivedAudio = null;
            _onReceivedVideo = null;
            _onReject = null;
            _onRequestTimeout = null;
            _onRinging = null;
            _onStart = null;
            _onPeerCSChange = null;
            _onSelfCSChange = null;

            OnReceivedGroupAudio = null;
        }

        internal void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// Kills this toxav instance.
        /// </summary>
        [Obsolete("Use Dispose() instead", true)]
        public void Kill()
        {
            if (_toxAv.IsClosed || _toxAv.IsInvalid)
                throw null;

            _toxAv.Dispose();
        }

        /// <summary>
        /// Starts the main toxav_do loop.
        /// </summary>
        public void Start()
        {
            CheckDisposed();

            if (_running)
                return;

            Loop();
        }

        /// <summary>
        /// Stops the main toxav_do loop if it's running.
        /// </summary>
        public void Stop()
        {
            CheckDisposed();

            if (!_running)
                return;

            if (_cancelTokenSource != null)
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();

                _running = false;
            }
        }

        private void Loop()
        {
            _cancelTokenSource = new CancellationTokenSource();
            _running = true;

            Task.Factory.StartNew(() =>
            {
                while (_running)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                        break;

                    int delay = DoIterate();

#if IS_PORTABLE
                    Task.Delay(delay);
#else
                    Thread.Sleep(delay);
#endif
                }
            }, _cancelTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Runs the loop once in the current thread and returns the next timeout.
        /// </summary>
        public int Iterate()
        {
            CheckDisposed();

            if (_running)
                throw new Exception("Loop already running");

            return DoIterate();
        }

        private int DoIterate()
        {
            ToxAvFunctions.Do(_toxAv);
            return (int)ToxAvFunctions.DoInterval(_toxAv);
        }

        /// <summary>
        /// Retrieves a peer's codec settings.
        /// </summary>
        /// <param name="callIndex"></param>
        /// <param name="peer"></param>
        /// <returns></returns>
        public ToxAvCodecSettings GetPeerCodecSettings(int callIndex, int peer)
        {
            CheckDisposed();

            ToxAvCodecSettings settings = new ToxAvCodecSettings();
            ToxAvFunctions.GetPeerCodecSettings(_toxAv, callIndex, peer, ref settings);

            return settings;
        }

        /// <summary>
        /// Creates a new audio groupchat.
        /// </summary>
        /// <returns></returns>
        public int AddAvGroupchat()
        {
            CheckDisposed();

            ToxAvDelegates.GroupAudioReceiveCallback callback = (IntPtr tox, int groupNumber, int peerNumber, IntPtr frame, uint sampleCount, byte channels, uint sampleRate, IntPtr userData) =>
            {
                if (OnReceivedGroupAudio != null)
                {
                    short[] samples = new short[sampleCount * channels];
                    Marshal.Copy(frame, samples, 0, samples.Length);

                    OnReceivedGroupAudio(this, new ToxAvEventArgs.GroupAudioDataEventArgs(groupNumber, peerNumber, samples, (int)channels, (int)sampleRate));
                }
            };

            int result = ToxAvFunctions.AddAvGroupchat(Tox.Handle, callback, IntPtr.Zero);
            if (result != -1)
                _groupAudioHandlers.Add(callback);

            return result;
        }

        /// <summary>
        /// Joins an audio groupchat.
        /// </summary>
        /// <param name="friendNumber"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public int JoinAvGroupchat(int friendNumber, byte[] data)
        {
            CheckDisposed();

            ToxAvDelegates.GroupAudioReceiveCallback callback = (IntPtr tox, int groupNumber, int peerNumber, IntPtr frame, uint sampleCount, byte channels, uint sampleRate, IntPtr userData) =>
            {
                if (OnReceivedGroupAudio != null)
                {
                    short[] samples = new short[sampleCount * channels];
                    Marshal.Copy(frame, samples, 0, samples.Length);

                    OnReceivedGroupAudio(this, new ToxAvEventArgs.GroupAudioDataEventArgs(groupNumber, peerNumber, samples, (int)channels, (int)sampleRate));
                }
            };

            int result = ToxAvFunctions.JoinAvGroupchat(Tox.Handle, friendNumber, data, (ushort)data.Length, callback, IntPtr.Zero);
            if (result != -1)
                _groupAudioHandlers.Add(callback);

            return result;
        }

        /// <summary>
        /// Sends an audio frame to a group.
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <param name="pcm"></param>
        /// <param name="perframe"></param>
        /// <param name="channels"></param>
        /// <param name="sampleRate"></param>
        /// <returns></returns>
        public bool GroupSendAudio(int groupNumber, short[] pcm, int perframe, int channels, int sampleRate)
        {
            CheckDisposed();

            return ToxAvFunctions.GroupSendAudio(Tox.Handle, groupNumber, pcm, (uint)perframe, (byte)channels, (uint)sampleRate) == 0;
        }

        private Dictionary<int, ToxAvCall> _calls = new Dictionary<int, ToxAvCall>();
        public ToxAvCall CallFromCallIndex(int callIndex)
        {
            ToxAvCall call;
            if (_calls.TryGetValue(callIndex, out call))
                return call;
            call = new ToxAvCall(this, callIndex);
            _calls[callIndex] = call;
            return call;
        }

        public ToxAvCall[] Calls
        {
            get
            {
                return _calls.Values.ToArray();
            }
        }

        #region Events
        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onCancel;

        /// <summary>
        /// Occurs when a call gets canceled.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnCancel
        {
            add
            {
                if (_onCancelCallback == null)
                {
                    _onCancelCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onCancel(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnCancel));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onCancelCallback, ToxAvCallbackID.OnCancel, IntPtr.Zero);
                }

                _onCancel += value;
            }
            remove
            {
                if (_onCancel.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnCancel, IntPtr.Zero);
                    _onCancelCallback = null;
                }

                _onCancel -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onEnd;

        /// <summary>
        /// Occurs when a call ends.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnEnd
        {
            add
            {
                if (_onEndCallback == null)
                {
                    _onEndCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onEnd(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnEnd));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onEndCallback, ToxAvCallbackID.OnEnd, IntPtr.Zero);
                }

                _onEnd += value;
            }
            remove
            {
                if (_onEnd.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnEnd, IntPtr.Zero);
                    _onEndCallback = null;
                }

                _onEnd -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onInvite;

        /// <summary>
        /// Occurs when an invite for a call is received.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnInvite
        {
            add
            {
                if (_onInviteCallback == null)
                {
                    _onInviteCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onInvite(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnInvite));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onInviteCallback, ToxAvCallbackID.OnInvite, IntPtr.Zero);
                }

                _onInvite += value;
            }
            remove
            {
                if (_onInvite.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnInvite, IntPtr.Zero);
                    _onInviteCallback = null;
                }

                _onInvite -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onPeerTimeout;

        /// <summary>
        /// Occurs when the person on the other end timed out.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnPeerTimeout
        {
            add
            {
                if (_onPeerTimeoutCallback == null)
                {
                    _onPeerTimeoutCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onPeerTimeout(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnPeerTimeout));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onPeerTimeoutCallback, ToxAvCallbackID.OnPeerTimeout, IntPtr.Zero);
                }

                _onPeerTimeout += value;
            }
            remove
            {
                if (_onPeerTimeout.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnPeerTimeout, IntPtr.Zero);
                    _onPeerTimeoutCallback = null;
                }

                _onPeerTimeout -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onReject;

        /// <summary>
        /// Occurs when a call gets rejected.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnReject
        {
            add
            {
                if (_onRejectCallback == null)
                {
                    _onRejectCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onReject(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnReject));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onRejectCallback, ToxAvCallbackID.OnReject, IntPtr.Zero);
                }

                _onReject += value;
            }
            remove
            {
                if (_onReject.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnReject, IntPtr.Zero);
                    _onRejectCallback = null;
                }

                _onReject -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onRequestTimeout;

        /// <summary>
        /// Occurs when a call request times out.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnRequestTimeout
        {
            add
            {
                if (_onRequestTimeoutCallback == null)
                {
                    _onRequestTimeoutCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onRequestTimeout(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnRequestTimeout));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onRequestTimeoutCallback, ToxAvCallbackID.OnRequestTimeout, IntPtr.Zero);
                }

                _onRequestTimeout += value;
            }
            remove
            {
                if (_onRequestTimeout.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnRequestTimeout, IntPtr.Zero);
                    _onRequestTimeoutCallback = null;
                }

                _onRequestTimeout -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onRinging;

        /// <summary>
        /// Occurs when the person on the other end received the invite.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnRinging
        {
            add
            {
                if (_onRingingCallback == null)
                {
                    _onRingingCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onRinging(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnRinging));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onRingingCallback, ToxAvCallbackID.OnRinging, IntPtr.Zero);
                }

                _onRinging += value;
            }
            remove
            {
                if (_onRinging.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnRinging, IntPtr.Zero);
                    _onRingingCallback = null;
                }

                _onRinging -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onStart;

        /// <summary>
        /// Occurs when the call is supposed to start.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnStart
        {
            add
            {
                if (_onStartCallback == null)
                {
                    _onStartCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onStart(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnStart));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onStartCallback, ToxAvCallbackID.OnStart, IntPtr.Zero);
                }

                _onStart += value;
            }
            remove
            {
                if (_onStart.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnStart, IntPtr.Zero);
                    _onStartCallback = null;
                }

                _onStart -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onPeerCSChange;

        /// <summary>
        /// Occurs when a peer wants to change the call type.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnPeerCodecSettingsChanged
        {
            add
            {
                if (_onPeerCSChangeCallback == null)
                {
                    _onPeerCSChangeCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onPeerCSChange(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnPeerCSChange));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onPeerCSChangeCallback, ToxAvCallbackID.OnPeerCSChange, IntPtr.Zero);
                }

                _onPeerCSChange += value;
            }
            remove
            {
                if (_onPeerCSChange.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnPeerCSChange, IntPtr.Zero);
                    _onPeerCSChangeCallback = null;
                }

                _onPeerCSChange -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.CallStateEventArgs> _onSelfCSChange;

        /// <summary>
        /// Occurs when a peer wants to change the call type.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.CallStateEventArgs> OnSelfCodecSettingsChanged
        {
            add
            {
                if (_onSelfCSChangeCallback == null)
                {
                    _onSelfCSChangeCallback = (IntPtr agent, int callIndex, IntPtr args) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onSelfCSChange(this, new ToxAvEventArgs.CallStateEventArgs(call, ToxAvCallbackID.OnSelfCSChange));
                    };

                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, _onSelfCSChangeCallback, ToxAvCallbackID.OnSelfCSChange, IntPtr.Zero);
                }

                _onSelfCSChange += value;
            }
            remove
            {
                if (_onSelfCSChange.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterCallstateCallback(_toxAv, null, ToxAvCallbackID.OnSelfCSChange, IntPtr.Zero);
                    _onSelfCSChangeCallback = null;
                }

                _onSelfCSChange -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.AudioDataEventArgs> _onReceivedAudio;

        /// <summary>
        /// Occurs when an audio frame was received.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.AudioDataEventArgs> OnReceivedAudio
        {
            add
            {
                if (_onReceivedAudioCallback == null)
                {
                    _onReceivedAudioCallback = (IntPtr ptr, int callIndex, IntPtr frame, int frameSize, IntPtr userData) =>
                    {
                        int channels = (int)GetPeerCodecSettings(callIndex, 0).AudioChannels;
                        short[] samples = new short[frameSize * channels];

                        Marshal.Copy(frame, samples, 0, samples.Length);

                        var call = CallFromCallIndex(callIndex);
                        _onReceivedAudio(this, new ToxAvEventArgs.AudioDataEventArgs(call, samples));
                    };

                    ToxAvFunctions.RegisterAudioReceiveCallback(_toxAv, _onReceivedAudioCallback, IntPtr.Zero);
                }

                _onReceivedAudio += value;
            }
            remove
            {
                if (_onReceivedAudio.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterAudioReceiveCallback(_toxAv, null, IntPtr.Zero);
                    _onReceivedAudioCallback = null;
                }

                _onReceivedAudio -= value;
            }
        }

        private EventHandler<ToxAvEventArgs.VideoDataEventArgs> _onReceivedVideo;

        /// <summary>
        /// Occurs when a video frame was received.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.VideoDataEventArgs> OnReceivedVideo
        {
            add
            {
                if (_onReceivedVideoCallback == null)
                {
                    _onReceivedVideoCallback = (IntPtr ptr, int callIndex, IntPtr frame, IntPtr userData) =>
                    {
                        var call = CallFromCallIndex(callIndex);
                        _onReceivedVideo(this, new ToxAvEventArgs.VideoDataEventArgs(call, frame));
                    };

                    ToxAvFunctions.RegisterVideoReceiveCallback(_toxAv, _onReceivedVideoCallback, IntPtr.Zero);
                }

                _onReceivedVideo += value;
            }
            remove
            {
                if (_onReceivedVideo.GetInvocationList().Length == 1)
                {
                    ToxAvFunctions.RegisterVideoReceiveCallback(_toxAv, null, IntPtr.Zero);
                    _onReceivedVideoCallback = null;
                }

                _onReceivedVideo -= value;
            }
        }

        /// <summary>
        /// Occurs when an audio was received from a group.
        /// </summary>
        public event EventHandler<ToxAvEventArgs.GroupAudioDataEventArgs> OnReceivedGroupAudio;

        #endregion
    }
}
