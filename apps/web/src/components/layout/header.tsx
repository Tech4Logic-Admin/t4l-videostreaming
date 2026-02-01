'use client';

import Link from 'next/link';
import { Search, Upload, LayoutDashboard, User, Menu, Shield, FileVideo, LogOut } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { DevUserSelector } from '@/components/auth';
import { useAuth } from '@/lib/auth';
import { usePermissions } from '@/components/auth/protected';

export function Header() {
  const { user, isAuthenticated, isLoading, logout } = useAuth();
  const { canUpload, canReview, isAdmin } = usePermissions();

  const handleLogout = async () => {
    await logout();
  };

  return (
    <header className="sticky top-0 z-50 w-full border-b bg-white/95 backdrop-blur supports-[backdrop-filter]:bg-white/60">
      <div className="container mx-auto flex h-16 items-center justify-between px-4">
        {/* Logo */}
        <Link href="/" className="flex items-center space-x-2">
          <div className="h-10 w-10 bg-t4l-primary rounded flex items-center justify-center">
            <span className="text-white font-bold text-lg">T4</span>
          </div>
          <span className="font-semibold text-t4l-dark hidden sm:inline-block">
            Video Search
          </span>
        </Link>

        {/* Navigation - Role-based */}
        <nav className="hidden md:flex items-center space-x-6">
          {/* Search - available to all authenticated users */}
          {isAuthenticated && (
            <Link
              href="/search"
              className="flex items-center text-sm font-medium text-gray-600 hover:text-t4l-primary transition-colors"
            >
              <Search className="mr-1 h-4 w-4" />
              Search
            </Link>
          )}

          {/* Upload - only for uploaders and admins */}
          {canUpload && (
            <Link
              href="/upload"
              className="flex items-center text-sm font-medium text-gray-600 hover:text-t4l-primary transition-colors"
            >
              <Upload className="mr-1 h-4 w-4" />
              Upload
            </Link>
          )}

          {/* Review Queue - only for reviewers and admins */}
          {canReview && (
            <Link
              href="/review"
              className="flex items-center text-sm font-medium text-gray-600 hover:text-t4l-primary transition-colors"
            >
              <Shield className="mr-1 h-4 w-4" />
              Review
            </Link>
          )}

          {/* Dashboard - only for admins */}
          {isAdmin && (
            <Link
              href="/admin/dashboard"
              className="flex items-center text-sm font-medium text-gray-600 hover:text-t4l-primary transition-colors"
            >
              <LayoutDashboard className="mr-1 h-4 w-4" />
              Dashboard
            </Link>
          )}
        </nav>

        {/* Right side - Dev selector + User Menu */}
        <div className="flex items-center space-x-3">
          {/* Dev User Selector */}
          <DevUserSelector />

          {/* User Menu */}
          {isAuthenticated && user ? (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" className="relative h-9 w-9 rounded-full">
                  <div className="h-9 w-9 rounded-full bg-t4l-primary flex items-center justify-center">
                    <User className="h-5 w-5 text-white" />
                  </div>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-56" align="end" forceMount>
                <DropdownMenuLabel className="font-normal">
                  <div className="flex flex-col space-y-1">
                    <p className="text-sm font-medium leading-none">{user.name}</p>
                    <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
                    <div className="flex gap-1 mt-1">
                      {user.roles.map((role) => (
                        <span
                          key={role}
                          className="text-xs bg-t4l-primary/10 text-t4l-primary px-1.5 py-0.5 rounded"
                        >
                          {role}
                        </span>
                      ))}
                    </div>
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />

                {/* My Videos - for uploaders */}
                {canUpload && (
                  <DropdownMenuItem asChild>
                    <Link href="/my-videos" className="flex items-center">
                      <FileVideo className="mr-2 h-4 w-4" />
                      My Videos
                    </Link>
                  </DropdownMenuItem>
                )}

                {/* Admin links */}
                {isAdmin && (
                  <>
                    <DropdownMenuItem asChild>
                      <Link href="/admin/dashboard" className="flex items-center">
                        <LayoutDashboard className="mr-2 h-4 w-4" />
                        Dashboard
                      </Link>
                    </DropdownMenuItem>
                    <DropdownMenuItem asChild>
                      <Link href="/admin/audit" className="flex items-center">
                        <Shield className="mr-2 h-4 w-4" />
                        Audit Logs
                      </Link>
                    </DropdownMenuItem>
                  </>
                )}

                <DropdownMenuSeparator />
                <DropdownMenuItem
                  className="text-red-600 cursor-pointer"
                  onClick={handleLogout}
                >
                  <LogOut className="mr-2 h-4 w-4" />
                  Logout
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          ) : !isLoading ? (
            <Link href="/login">
              <Button>Sign In</Button>
            </Link>
          ) : null}

          {/* Mobile menu */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild className="md:hidden">
              <Button variant="ghost" size="icon">
                <Menu className="h-5 w-5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-48">
              {isAuthenticated && (
                <DropdownMenuItem asChild>
                  <Link href="/search" className="flex items-center">
                    <Search className="mr-2 h-4 w-4" />
                    Search
                  </Link>
                </DropdownMenuItem>
              )}
              {canUpload && (
                <DropdownMenuItem asChild>
                  <Link href="/upload" className="flex items-center">
                    <Upload className="mr-2 h-4 w-4" />
                    Upload
                  </Link>
                </DropdownMenuItem>
              )}
              {canReview && (
                <DropdownMenuItem asChild>
                  <Link href="/review" className="flex items-center">
                    <Shield className="mr-2 h-4 w-4" />
                    Review
                  </Link>
                </DropdownMenuItem>
              )}
              {isAdmin && (
                <DropdownMenuItem asChild>
                  <Link href="/admin/dashboard" className="flex items-center">
                    <LayoutDashboard className="mr-2 h-4 w-4" />
                    Dashboard
                  </Link>
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  );
}
