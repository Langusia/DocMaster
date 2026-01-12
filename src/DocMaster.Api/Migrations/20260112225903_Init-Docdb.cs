using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMaster.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitDocdb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buckets",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buckets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    grpc_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_healthy = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    total_space_bytes = table.Column<long>(type: "bigint", nullable: true),
                    free_space_bytes = table.Column<long>(type: "bigint", nullable: true),
                    used_space_bytes = table.Column<long>(type: "bigint", nullable: true),
                    object_count = table.Column<int>(type: "integer", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objects",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    bucket_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    detected_content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    claimed_content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    detected_extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    original_filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    storage_strategy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    chunk_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_objects", x => x.id);
                    table.ForeignKey(
                        name: "FK_objects_buckets_bucket_id",
                        column: x => x.bucket_id,
                        principalTable: "buckets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chunks",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    object_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_chunks_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "replicas",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    object_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    node_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replicas", x => x.id);
                    table.ForeignKey(
                        name: "FK_replicas_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_replicas_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shards",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    chunk_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    shard_index = table.Column<int>(type: "integer", nullable: false),
                    node_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shards", x => x.id);
                    table.ForeignKey(
                        name: "FK_shards_chunks_chunk_id",
                        column: x => x.chunk_id,
                        principalTable: "chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shards_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buckets_name",
                table: "buckets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chunks_object_id",
                table: "chunks",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "IX_chunks_object_id_chunk_index",
                table: "chunks",
                columns: new[] { "object_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodes_is_healthy",
                table: "nodes",
                column: "is_healthy");

            migrationBuilder.CreateIndex(
                name: "IX_objects_bucket_id_key",
                table: "objects",
                columns: new[] { "bucket_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_objects_status",
                table: "objects",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_replicas_node_id",
                table: "replicas",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "IX_replicas_object_id",
                table: "replicas",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "IX_replicas_object_id_node_id",
                table: "replicas",
                columns: new[] { "object_id", "node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shards_chunk_id",
                table: "shards",
                column: "chunk_id");

            migrationBuilder.CreateIndex(
                name: "IX_shards_chunk_id_shard_index",
                table: "shards",
                columns: new[] { "chunk_id", "shard_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shards_node_id",
                table: "shards",
                column: "node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "replicas");

            migrationBuilder.DropTable(
                name: "shards");

            migrationBuilder.DropTable(
                name: "chunks");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "objects");

            migrationBuilder.DropTable(
                name: "buckets");
        }
    }
}
