# SR ACL input buffer alignment

## Summary

Some `AnimationClip` assets failed during converted export with an exception from
`Marshal.Copy`:

```text
Value cannot be null. (Parameter 'source')
at ACLLibs.SRACL.DecompressAll(...)
```

The affected clips were valid. The failure was caused by passing a managed
`byte[]` directly to the native SR ACL decoder even though the compressed clip
input must be 16-byte aligned.

## Root cause

`SRACL.DecompressAll` previously declared the native entry point like this:

```csharp
private static extern void DecompressClip(byte[] data, ref DecompressedClip decompressedClip);
```

P/Invoke pins the managed array for the call, but it does not guarantee that the
array starts at a 16-byte-aligned address. Consequently, decoding depended on the
address selected by the managed allocator:

- aligned arrays decoded normally;
- unaligned arrays were rejected by the native ACL decoder;
- the native wrapper returned without populating `Values` and `Times`;
- `Marshal.Copy` then received `IntPtr.Zero` and reported a null `source`.

This also explains why the GUI's final message could be misleading. Export
exceptions and existing output files are both included in the generic
`not extractable or files already exist` skipped count.

## Fix

The SR wrapper now:

1. Allocates an unmanaged buffer with 15 bytes of alignment padding.
2. Rounds its address up to the next 16-byte boundary.
3. Copies the compressed clip into the aligned buffer.
4. Passes the aligned pointer to the native decoder.
5. Validates native output before calling `Marshal.Copy`.
6. Releases both native decoder output and the temporary input buffer in a
   `finally` block.

The native import therefore accepts `nint` instead of a managed `byte[]`.

## Verification

The issue was reproduced with 39 `AnimationClip` assets from one block using the
CLI converted-export path.

| Build | Exported | Failed |
| --- | ---: | ---: |
| Baseline | 17 | 22 |
| Aligned input | 39 | 0 |

Both runs used the same input block, game selection, asset filter, export mode,
and native decoder. The only behavioral change was alignment and validation in
`SRACL.DecompressAll`.

Representative command:

```powershell
AnimeStudio.CLI.exe SAMPLE.block OUTPUT_DIR `
  --game SR `
  --types AnimationClip:Both `
  --names '^CLIP_NAME_PREFIX' `
  --export_type Convert `
  --group_assets None
```

Excluding the complete block is not an appropriate workaround: the block can
contain both previously successful and previously failing clips, and all tested
clips exported after aligning the decoder input.
