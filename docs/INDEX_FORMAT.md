# Index behavior and recovery

The reference schema is SteamTracking's [`webuimessages_gamerecordingfiles.proto`](https://github.com/SteamTracking/Protobufs/blob/master/steam/webuimessages_gamerecordingfiles.proto). Valve may change this internal format without notice. Index editing is beta: tests validate file transformations, not a live Steam reload.

## What changes

- Root field `1` contains timelines. Within a timeline, field `5` contains recording metadata. A selected, recognized local `bg_…`/`fg_…` recording reference is removed after its folder is successfully deleted.
- Recording fields `6`, `7` and `8` are treated conservatively as remote references. Such a selected recording rejects synchronization during preflight; a missing-folder repair preserves it.
- Root field `3` lists temporary clip IDs. Root field `4` contains tags, with nested clip references. Explicitly selected clips can be removed from those lists.
- Missing-clip repair only acts on IDs explicitly present in the root temporary-clip list. A missing tag-only clip folder is not sufficient evidence that a remote/shared clip is invalid.

## What stays

All timeline objects, timeline durations, phases, significant events, post-game summaries and timeline JSON files remain. Unknown fields and unchanged messages keep their original bytes. A removed recording may leave a valid gameplay timeline with no video; this is intentional. Existing clip `clip.pb` files are not rewritten, although deleting an entire selected clip folder removes its contained files.

This does not rebuild indexes for orphan video folders, consolidate game history, or remove every obsolete thumbnail. Do not delete `gamerecording.pb` wholesale to “refresh” it.

## Write order and failure

For synchronized cleanup, all selected folders and the proposed index edits are checked before the batch starts. The original root index and timeline files are backed up to the batch history directory. Each successful folder deletion is followed by its corresponding index update. Each index update also saves its immediately preceding full index bytes under `index-before-<random>.pb`.

Writes use a fresh same-directory temporary file, flush, pre-replacement snapshot comparison, file replacement and readback verification. Concurrent changes or a detected Steam process stop the operation. These controls are not a complete filesystem lock against a malicious same-user process.

`START`, `INDEX`, `OK` and `FAILED` log entries identify progress. A failed item may already have lost its video folder before its index update fails; it may not be included in the success count. There is no automatic rollback of deletion.

## Recovery principles

Fully exit Steam first. Preserve the current index and logs before attempting recovery. For a whole synchronized batch, its `gamerecording.pb` backup contains the pre-batch index. For an index-only repair, its `index-before-…pb` backup contains the pre-repair index. Incremental backups are intermediate states, not interchangeable with the pre-batch snapshot.

Do not restore an old index over a newer one after additional recording activity. Backups do not contain video content, and restoring a folder from the Recycle Bin does not restore its index. A failed synchronization can often be addressed by previewing missing-local-reference repair, but do not assume every missing item is eligible.

## Parser limits

Index reads are limited to 64 MiB; parsing allows at most 100,000 fields per parsed message. Supported wire types are varint, fixed64, length-delimited and fixed32. Malformed lengths, overflowing varints and unsupported wire types fail closed. Unknown fields of supported wire types are retained.
