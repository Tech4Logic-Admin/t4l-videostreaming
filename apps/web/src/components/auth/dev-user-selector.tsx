'use client';

import { useState } from 'react';
import { useAuth } from '@/lib/auth';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

const roleColors: Record<string, string> = {
  Admin: 'bg-red-100 text-red-800',
  Uploader: 'bg-blue-100 text-blue-800',
  Reviewer: 'bg-yellow-100 text-yellow-800',
  Viewer: 'bg-green-100 text-green-800',
};

export function DevUserSelector() {
  const { user, isAuthenticated, isDevMode, devUsers, login, logout, isLoading } = useAuth();
  const [isOpen, setIsOpen] = useState(false);

  if (!isDevMode) {
    return null;
  }

  const handleSelectUser = async (userId: string, role: string) => {
    await login(userId, role);
    setIsOpen(false);
  };

  const handleLogout = async () => {
    await logout();
    setIsOpen(false);
  };

  return (
    <DropdownMenu open={isOpen} onOpenChange={setIsOpen}>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          className="gap-2 border-dashed border-orange-300 bg-orange-50 text-orange-700 hover:bg-orange-100"
          disabled={isLoading}
        >
          <span className="text-xs font-semibold uppercase">Dev</span>
          {isAuthenticated && user ? (
            <span className="flex items-center gap-2">
              <span className="max-w-[100px] truncate">{user.name}</span>
              <span
                className={`rounded px-1.5 py-0.5 text-xs font-medium ${roleColors[user.roles[0]] || 'bg-gray-100 text-gray-800'}`}
              >
                {user.roles[0]}
              </span>
            </span>
          ) : (
            <span>Select User</span>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="text-orange-600">
          🧪 Dev Authentication
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {devUsers.map((devUser) => (
          <DropdownMenuItem
            key={devUser.id}
            onClick={() => handleSelectUser(devUser.id, devUser.role)}
            className="flex items-center justify-between cursor-pointer"
          >
            <span className="flex flex-col">
              <span className="font-medium">{devUser.name}</span>
              <span className="text-xs text-muted-foreground">{devUser.email}</span>
            </span>
            <span
              className={`rounded px-1.5 py-0.5 text-xs font-medium ${roleColors[devUser.role] || 'bg-gray-100 text-gray-800'}`}
            >
              {devUser.role}
            </span>
          </DropdownMenuItem>
        ))}
        {isAuthenticated && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={handleLogout}
              className="text-red-600 cursor-pointer"
            >
              Logout
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
