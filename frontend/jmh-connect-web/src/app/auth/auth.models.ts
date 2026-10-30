export type ApplicationRole = 'Pastor' | 'Leader' | 'Member';

export interface AuthenticatedUser {
  id: string;
  email: string;
  role: ApplicationRole;
  memberProfileId?: number | null;
}

export interface LoginCredentials {
  email: string;
  password: string;
}

export interface CreateAccountRequest {
  email: string;
  role: Exclude<ApplicationRole, 'Pastor'>;
  memberProfileId?: number;
}

export interface AccountSetupRequest {
  userId: string;
  token: string;
  password: string;
}

export interface AntiforgeryResponse {
  requestToken: string;
}
