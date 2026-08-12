using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace MwcSaveSlots
{
internal enum TransactionCheckpoint
{
	StageCreated,
	StageCopied,
	StageVerified,
	PreviousTargetMoved,
	PreparedTargetMoved,
	MetadataCommitted,
	SourceMovedForDelete,
	CleanupStarted
}

internal sealed class SnapshotTransaction
{
	private readonly string stagingRoot;
	private readonly Action<string, string> logger;
	private readonly Action<TransactionCheckpoint> checkpoint;

	internal SnapshotTransaction(string stagingRoot, Action<string, string> logger, Action<TransactionCheckpoint> checkpoint)
	{
		this.stagingRoot = Path.GetFullPath(stagingRoot);
		this.logger = logger;
		this.checkpoint = checkpoint;
	}

	internal string CreateEmptyStage(string purpose)
	{
		Directory.CreateDirectory(stagingRoot);
		string path;
		do
		{
			path = Path.Combine(stagingRoot, DateTime.UtcNow.Ticks + "-" + Guid.NewGuid().ToString("N") + "-" + SafeName(purpose));
		}
		while (Directory.Exists(path));
		Directory.CreateDirectory(path);
		try
		{
			Signal(TransactionCheckpoint.StageCreated);
		}
		catch
		{
			TryDeleteTree(path, "discard stage rejected at creation");
			throw;
		}
		return path;
	}

	internal string PrepareCopy(string source, string purpose, Predicate<string> includeName, bool requirePlayableSave)
	{
		if (!Directory.Exists(source))
		{
			throw new DirectoryNotFoundException("Snapshot source does not exist: " + source);
		}

		string stage = CreateEmptyStage(purpose);
		try
		{
			CopyTree(source, stage, includeName);
			Signal(TransactionCheckpoint.StageCopied);
			VerifyTree(source, stage, includeName, requirePlayableSave);
			Signal(TransactionCheckpoint.StageVerified);
			return stage;
		}
		catch
		{
			TryDeleteTree(stage, "discard failed stage");
			throw;
		}
	}

	internal void ReplaceSnapshot(string source, string target, string purpose, Predicate<string> includeName, bool requirePlayableSave, Action<string> decoratePrepared)
	{
		string prepared = PrepareCopy(source, purpose, includeName, requirePlayableSave);
		string rollback = null;
		bool movedPrepared = false;
		try
		{
			if (decoratePrepared != null)
			{
				decoratePrepared(prepared);
			}

			if (Directory.Exists(target))
			{
				rollback = UniqueSibling(target, ".rollback-");
				Directory.Move(target, rollback);
				Signal(TransactionCheckpoint.PreviousTargetMoved);
			}

			Directory.Move(prepared, target);
			prepared = null;
			movedPrepared = true;
			Signal(TransactionCheckpoint.PreparedTargetMoved);
		}
		catch
		{
			if (movedPrepared && Directory.Exists(target))
			{
				TryDeleteTree(target, "remove incomplete replacement");
			}
			if (rollback != null && Directory.Exists(rollback) && !Directory.Exists(target))
			{
				Directory.Move(rollback, target);
				rollback = null;
			}
			throw;
		}
		finally
		{
			if (prepared != null && Directory.Exists(prepared))
			{
				TryDeleteTree(prepared, "clean uncommitted stage");
			}
		}

		if (rollback != null && Directory.Exists(rollback))
		{
			SignalCleanup();
			TryDeleteTree(rollback, "clean committed snapshot rollback");
		}
	}

	internal void CommitPreparedDirectory(string prepared, string target, Action afterMove, Action afterRollback)
	{
		string rollback = null;
		bool preparedMoved = false;
		try
		{
			if (Directory.Exists(target))
			{
				rollback = UniqueSibling(target, ".mwcslots-rollback-");
				Directory.Move(target, rollback);
				Signal(TransactionCheckpoint.PreviousTargetMoved);
			}

			Directory.Move(prepared, target);
			preparedMoved = true;
			Signal(TransactionCheckpoint.PreparedTargetMoved);
			if (afterMove != null)
			{
				afterMove();
			}
			Signal(TransactionCheckpoint.MetadataCommitted);
		}
		catch
		{
			if (preparedMoved && Directory.Exists(target))
			{
				TryDeleteTree(target, "remove uncommitted active save");
			}
			if (rollback != null && Directory.Exists(rollback) && !Directory.Exists(target))
			{
				Directory.Move(rollback, target);
				rollback = null;
			}
			if (afterRollback != null)
			{
				afterRollback();
			}
			throw;
		}

		if (rollback != null && Directory.Exists(rollback))
		{
			SignalCleanup();
			TryDeleteTree(rollback, "clean committed active rollback");
		}
	}

	internal void VerifyTree(string source, string copy, Predicate<string> includeName, bool requirePlayableSave)
	{
		if (requirePlayableSave)
		{
			string sourceSave = Path.Combine(source, "savefile.txt");
			string copiedSave = Path.Combine(copy, "savefile.txt");
			if (!File.Exists(sourceSave) || !File.Exists(copiedSave) || !FilesEqual(sourceSave, copiedSave, true))
			{
				throw new IOException("Playable save verification failed for savefile.txt.");
			}
		}

		Dictionary<string, FileStamp> sourceIndex = IndexTree(source, includeName);
		Dictionary<string, FileStamp> copyIndex = IndexTree(copy, includeName);
		if (sourceIndex.Count != copyIndex.Count)
		{
			throw new IOException("Snapshot verification failed: file count differs.");
		}
		foreach (KeyValuePair<string, FileStamp> item in sourceIndex)
		{
			FileStamp copied;
			if (!copyIndex.TryGetValue(item.Key, out copied) || copied.Length != item.Value.Length)
			{
				throw new IOException("Snapshot verification failed at " + item.Key + ".");
			}
			if (IsCriticalFile(Path.GetFileName(item.Key)) && !FilesEqual(item.Value.FullPath, copied.FullPath, true))
			{
				throw new IOException("Critical checksum failed at " + item.Key + ".");
			}
		}
	}

	internal void CopyTree(string source, string target, Predicate<string> includeName)
	{
		DirectoryInfo sourceInfo = new DirectoryInfo(source);
		if (!sourceInfo.Exists)
		{
			return;
		}
		Directory.CreateDirectory(target);

		foreach (FileInfo file in sourceInfo.GetFiles())
		{
			if (includeName != null && !includeName(file.Name))
			{
				continue;
			}
			string destination = Path.Combine(target, file.Name);
			file.CopyTo(destination, true);
			File.SetAttributes(destination, FileAttributes.Normal);
			File.SetLastWriteTimeUtc(destination, file.LastWriteTimeUtc);
		}

		foreach (DirectoryInfo directory in sourceInfo.GetDirectories())
		{
			if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
			{
				Log("CopyTree", "Skipped reparse-point directory " + directory.FullName);
				continue;
			}
			if (includeName != null && !includeName(directory.Name))
			{
				continue;
			}
			CopyTree(directory.FullName, Path.Combine(target, directory.Name), includeName);
		}
	}

	internal void DeleteTree(string path)
	{
		if (!Directory.Exists(path))
		{
			return;
		}
		foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
		{
			File.SetAttributes(file, FileAttributes.Normal);
		}
		Directory.Delete(path, true);
	}

	internal void MoveToTrash(string target, string purpose)
	{
		if (!Directory.Exists(target)) return;
		Directory.CreateDirectory(stagingRoot);
		string trash = UniquePathInStaging("delete-" + purpose);
		bool moved = false;
		try
		{
			Directory.Move(target, trash);
			moved = true;
			Signal(TransactionCheckpoint.SourceMovedForDelete);
		}
		catch
		{
			if (moved && Directory.Exists(trash) && !Directory.Exists(target))
			{
				Directory.Move(trash, target);
			}
			throw;
		}

		SignalCleanup();
		TryDeleteTree(trash, "clean verified deletion tombstone");
	}

	internal void RecoverMissingTarget(string target)
	{
		if (Directory.Exists(target))
		{
			return;
		}
		string parent = Path.GetDirectoryName(target);
		if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
		{
			return;
		}
		DirectoryInfo parentInfo = new DirectoryInfo(parent);
		DirectoryInfo[] activeCandidates = parentInfo.GetDirectories(Path.GetFileName(target) + ".mwcslots-rollback-*");
		DirectoryInfo[] snapshotCandidates = parentInfo.GetDirectories(Path.GetFileName(target) + ".rollback-*");
		DirectoryInfo[] candidates = new DirectoryInfo[activeCandidates.Length + snapshotCandidates.Length];
		activeCandidates.CopyTo(candidates, 0);
		snapshotCandidates.CopyTo(candidates, activeCandidates.Length);
		if (candidates.Length == 0)
		{
			return;
		}
		Array.Sort(candidates, delegate(DirectoryInfo left, DirectoryInfo right)
		{
			return right.CreationTimeUtc.CompareTo(left.CreationTimeUtc);
		});
		Directory.Move(candidates[0].FullName, target);
		Log("Recovery", "Recovered active save from " + candidates[0].FullName);
	}

	internal void CleanupOrphanStages()
	{
		if (!Directory.Exists(stagingRoot)) return;
		DirectoryInfo[] stages = new DirectoryInfo(stagingRoot).GetDirectories();
		for (int i = 0; i < stages.Length; i++)
		{
			TryDeleteTree(stages[i].FullName, "clean orphaned staging directory");
		}
	}

	internal static string Sha256(string path)
	{
		using (FileStream stream = File.OpenRead(path))
		using (SHA256 hash = SHA256.Create())
		{
			return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
		}
	}

	private static Dictionary<string, FileStamp> IndexTree(string root, Predicate<string> includeName)
	{
		Dictionary<string, FileStamp> result = new Dictionary<string, FileStamp>(StringComparer.OrdinalIgnoreCase);
		if (!Directory.Exists(root))
		{
			return result;
		}
		string normalizedRoot = Normalize(root);
		foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
		{
			string relative = path.Substring(normalizedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			bool included = true;
			if (includeName != null)
			{
				for (int i = 0; i < segments.Length; i++)
				{
					if (!includeName(segments[i]))
					{
						included = false;
						break;
					}
				}
			}
			if (included)
			{
				FileInfo info = new FileInfo(path);
				result.Add(relative, new FileStamp(info.FullName, info.Length));
			}
		}
		return result;
	}

	private static bool FilesEqual(string left, string right, bool checksum)
	{
		FileInfo leftInfo = new FileInfo(left);
		FileInfo rightInfo = new FileInfo(right);
		return leftInfo.Length == rightInfo.Length && (!checksum || string.Equals(Sha256(left), Sha256(right), StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsCriticalFile(string fileName)
	{
		return string.Equals(fileName, "savefile.txt", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, "carparts.txt", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, "SaveSlots.xml", StringComparison.OrdinalIgnoreCase);
	}

	private static string UniqueSibling(string path, string marker)
	{
		string candidate;
		do
		{
			candidate = path + marker + DateTime.UtcNow.Ticks + "-" + Guid.NewGuid().ToString("N");
		}
		while (Directory.Exists(candidate));
		return candidate;
	}

	private string UniquePathInStaging(string purpose)
	{
		string candidate;
		do
		{
			candidate = Path.Combine(stagingRoot, DateTime.UtcNow.Ticks + "-" + Guid.NewGuid().ToString("N") + "-" + SafeName(purpose));
		}
		while (Directory.Exists(candidate));
		return candidate;
	}

	private static string SafeName(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "operation";
		}
		char[] chars = value.ToCharArray();
		char[] invalid = Path.GetInvalidFileNameChars();
		for (int i = 0; i < chars.Length; i++)
		{
			for (int j = 0; j < invalid.Length; j++)
			{
				if (chars[i] == invalid[j])
				{
					chars[i] = '-';
					break;
				}
			}
		}
		return new string(chars);
	}

	private void TryDeleteTree(string path, string reason)
	{
		try
		{
			DeleteTree(path);
		}
		catch (Exception ex)
		{
			Log("Cleanup", reason + " failed at " + path + ": " + ex.Message);
		}
	}

	private void Signal(TransactionCheckpoint value)
	{
		if (checkpoint != null)
		{
			checkpoint(value);
		}
	}

	private void SignalCleanup()
	{
		try
		{
			Signal(TransactionCheckpoint.CleanupStarted);
		}
		catch (Exception ex)
		{
			Log("Cleanup", "A cleanup checkpoint failed after the transaction was committed: " + ex.Message);
		}
	}

	private void Log(string area, string message)
	{
		if (logger != null)
		{
			try { logger(area, message); } catch { }
		}
	}

	private static string Normalize(string path)
	{
		return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private struct FileStamp
	{
		internal readonly string FullPath;
		internal readonly long Length;

		internal FileStamp(string fullPath, long length)
		{
			FullPath = fullPath;
			Length = length;
		}
	}
}
}
