#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.IO.Pipes;
using System.Text;

namespace XerahS.Common
{
    public class SingleInstanceManager : IDisposable
    {
        public event Action<string[]>? ArgumentsReceived;

        public string MutexName { get; private set; }
        public string PipeName { get; private set; }
        public bool IsSingleInstance { get; private set; }
        public bool IsFirstInstance { get; private set; }

        private const int MaxArgumentsLength = 100;
        private const int ConnectTimeout = 5000;

        private Mutex? mutex;
        private bool ownsMutex;
        private FileStream? lockFileStream;
        private string? lockFilePath;
        private CancellationTokenSource? cts;

        private enum LockAcquireResult
        {
            Acquired,
            AlreadyHeldByAnotherInstance,
            Unavailable
        }

        public SingleInstanceManager(string mutexName, string pipeName, string[] args) : this(mutexName, pipeName, true, args)
        {
        }

        public SingleInstanceManager(string mutexName, string pipeName, bool isSingleInstance, string[] args)
        {
            MutexName = mutexName;
            PipeName = pipeName;
            IsSingleInstance = isSingleInstance;

            IsFirstInstance = !IsSingleInstance || TryAcquirePrimaryLock();

            if (IsSingleInstance)
            {
                if (IsFirstInstance)
                {
                    cts = new CancellationTokenSource();

                    Task.Run(ListenForConnectionsAsync, cts.Token);
                }
                else
                {
                    RedirectArgumentsToFirstInstance(args);
                    ReleaseInstanceLocks();
                }
            }
        }

        protected virtual void OnArgumentsReceived(string[] arguments)
        {
            ArgumentsReceived?.Invoke(arguments);
        }

        private async Task ListenForConnectionsAsync()
        {
            if (cts == null) return;

            while (!cts.IsCancellationRequested)
            {
                bool namedPipeServerCreated = false;

                try
                {
                    // Removed Windows specific PipeSecurity for cross-platform compatibility
                    // On Windows we might want to AddAccessRule but for now we simplify.

                    using (NamedPipeServerStream namedPipeServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        namedPipeServerCreated = true;

                        await namedPipeServer.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);

                        using (BinaryReader reader = new BinaryReader(namedPipeServer, Encoding.UTF8))
                        {
                            int length = reader.ReadInt32();

                            if (length < 0 || length > MaxArgumentsLength)
                            {
                                throw new Exception("Invalid length: " + length);
                            }

                            string[] args = new string[length];

                            for (int i = 0; i < length; i++)
                            {
                                args[i] = reader.ReadString();
                            }

                            OnArgumentsReceived(args);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    DebugHelper.WriteException(e);

                    if (!namedPipeServerCreated)
                    {
                        break;
                    }
                }
            }
        }

        private bool TryAcquirePrimaryLock()
        {
            var mutexResult = TryAcquireNamedMutex();
            var fileResult = TryAcquireLockFile();

            if (mutexResult == LockAcquireResult.AlreadyHeldByAnotherInstance ||
                fileResult == LockAcquireResult.AlreadyHeldByAnotherInstance)
            {
                DebugHelper.WriteLine("SingleInstanceManager: Existing instance detected (mutex and/or lock file held).");
                return false;
            }

            if (mutexResult == LockAcquireResult.Acquired && fileResult == LockAcquireResult.Acquired)
            {
                DebugHelper.WriteLine($"SingleInstanceManager: Acquired mutex '{MutexName}' and lock file '{lockFilePath}'.");
                return true;
            }

            if (mutexResult == LockAcquireResult.Acquired && fileResult == LockAcquireResult.Unavailable)
            {
                DebugHelper.WriteLine($"SingleInstanceManager: Lock file unavailable; proceeding with named mutex '{MutexName}' only.");
                return true;
            }

            if (mutexResult == LockAcquireResult.Unavailable && fileResult == LockAcquireResult.Acquired)
            {
                DebugHelper.WriteLine($"SingleInstanceManager: Named mutex unavailable; proceeding with lock file '{lockFilePath}' only.");
                return true;
            }

            DebugHelper.WriteLine("SingleInstanceManager: Could not acquire single-instance lock (mutex + lock file unavailable). Allowing startup as fallback.");
            return true;
        }

        private LockAcquireResult TryAcquireNamedMutex()
        {
            try
            {
                mutex = new Mutex(false, MutexName);

                try
                {
                    ownsMutex = mutex.WaitOne(0, false);
                    if (ownsMutex)
                    {
                        return LockAcquireResult.Acquired;
                    }
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                    return LockAcquireResult.Acquired;
                }

                mutex.Dispose();
                mutex = null;
                return LockAcquireResult.AlreadyHeldByAnotherInstance;
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, $"SingleInstanceManager: Failed to acquire named mutex '{MutexName}'.");
                mutex = null;
                ownsMutex = false;
                return LockAcquireResult.Unavailable;
            }
        }

        private LockAcquireResult TryAcquireLockFile()
        {
            try
            {
                lockFilePath = GetLockFilePath();
                lockFileStream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                string marker = $"{Environment.ProcessId}{Environment.NewLine}";
                lockFileStream.SetLength(0);
                byte[] bytes = Encoding.UTF8.GetBytes(marker);
                lockFileStream.Write(bytes, 0, bytes.Length);
                lockFileStream.Flush(true);
                lockFileStream.Position = 0;

                return LockAcquireResult.Acquired;
            }
            catch (IOException ioEx)
            {
                DebugHelper.WriteLine($"SingleInstanceManager: Lock file appears held by another instance: {ioEx.Message}");
                lockFileStream = null;
                return LockAcquireResult.AlreadyHeldByAnotherInstance;
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, "SingleInstanceManager: Failed to acquire lock file fallback.");
                lockFileStream = null;
                return LockAcquireResult.Unavailable;
            }
        }

        private string GetLockFilePath()
        {
            string tempPath = Path.GetTempPath();
            var sb = new StringBuilder(MutexName.Length);

            foreach (char c in MutexName)
            {
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
            }

            return Path.Combine(tempPath, $"{sb}.instance.lock");
        }

        private void RedirectArgumentsToFirstInstance(string[] args)
        {
            try
            {
                using (NamedPipeClientStream namedPipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    namedPipeClient.Connect(ConnectTimeout);

                    using (BinaryWriter writer = new BinaryWriter(namedPipeClient, Encoding.UTF8))
                    {
                        writer.Write(args.Length);

                        foreach (string argument in args)
                        {
                            writer.Write(argument);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }
        }

        public void Dispose()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            ReleaseInstanceLocks();
        }

        private void ReleaseInstanceLocks()
        {
            if (ownsMutex && mutex != null)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Ignore release failures when mutex ownership was already lost.
                }
            }

            ownsMutex = false;
            mutex?.Dispose();
            mutex = null;

            lockFileStream?.Dispose();
            lockFileStream = null;
        }
    }
}
