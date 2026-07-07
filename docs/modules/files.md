# Files Module

`Files` is an optional tenant-scoped API front door over shared file storage.

The module profile requires:

- `tenancy.context`
- `file-management.storage`

It provides:

- `files.objects`

Use it when a host wants a centralized upload/download/delete surface for private user files such as profile images, attachments, imports, or exports. Feature modules can bypass the front door and use `Gma.Framework.FileManagement` directly when they own public files, cross-user sharing, or business-specific file lifecycle rules.

## Endpoints

```text
POST   /api/files
GET    /api/files/{fileId}
DELETE /api/files/{fileId}
```

All endpoints require authorization. When tenancy is enabled, the standard tenant header is required and must match the authenticated token tenant claim. Storage keys are partitioned by tenant hash and caller subject hash, so another authenticated user in the same tenant cannot read or delete a private file by guessing its id.

## Composition Example

```csharp
builder.AddLocalFileStorage();
builder.AddModule<FilesModule>();
builder.ValidateModuleComposition();
```

For MinIO, use `builder.AddMinioFileStorage()` instead, or register both adapters and let `FileManagement:Provider` select one.
