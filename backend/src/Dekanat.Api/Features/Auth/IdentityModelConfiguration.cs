using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Auth
{
    // Таблицы Identity называем коротко и по-нашему: users, roles и так далее, а не asp_net_users.

    public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure( EntityTypeBuilder<AppUser> builder )
        {
            builder.ToTable( "users" );
            builder.HasIndex( user => user.NormalizedUserName ).HasDatabaseName( "ix_users_normalized_user_name" ).IsUnique();
            builder.HasIndex( user => user.NormalizedEmail ).HasDatabaseName( "ix_users_normalized_email" );
        }
    }

    public sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
    {
        public void Configure( EntityTypeBuilder<IdentityRole<Guid>> builder )
        {
            builder.ToTable( "roles" );
            builder.HasIndex( role => role.NormalizedName ).HasDatabaseName( "ix_roles_normalized_name" ).IsUnique();
            builder.HasMany<IdentityRoleClaim<Guid>>()
                .WithOne()
                .HasForeignKey( claim => claim.RoleId )
                .HasConstraintName( "fk_role_claims_roles_role_id" )
                .IsRequired();
            builder.HasData(
                new IdentityRole<Guid>
                {
                    Id = Roles.StudentId,
                    Name = Roles.Student,
                    NormalizedName = Roles.Student.ToUpperInvariant(),
                    ConcurrencyStamp = "a3c1e6f2-5b7d-4e9a-8c2f-1d3b5a7c9e01",
                },
                new IdentityRole<Guid>
                {
                    Id = Roles.StaffId,
                    Name = Roles.Staff,
                    NormalizedName = Roles.Staff.ToUpperInvariant(),
                    ConcurrencyStamp = "a3c1e6f2-5b7d-4e9a-8c2f-1d3b5a7c9e02",
                } );
        }
    }

    public sealed class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
    {
        public void Configure( EntityTypeBuilder<IdentityUserRole<Guid>> builder )
        {
            builder.ToTable( "user_roles" );
        }
    }

    public sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
    {
        public void Configure( EntityTypeBuilder<IdentityUserClaim<Guid>> builder )
        {
            builder.ToTable( "user_claims" );
        }
    }

    public sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
    {
        public void Configure( EntityTypeBuilder<IdentityUserLogin<Guid>> builder )
        {
            builder.ToTable( "user_logins" );
        }
    }

    public sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
    {
        public void Configure( EntityTypeBuilder<IdentityUserToken<Guid>> builder )
        {
            builder.ToTable( "user_tokens" );
        }
    }

    public sealed class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
    {
        public void Configure( EntityTypeBuilder<IdentityRoleClaim<Guid>> builder )
        {
            builder.ToTable( "role_claims" );
        }
    }
}
