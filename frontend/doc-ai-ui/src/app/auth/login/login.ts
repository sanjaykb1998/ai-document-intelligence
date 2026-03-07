import { Component } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class LoginComponent {
  loginForm: any;
  loginMessage = '';
  loginMessageType: 'success' | 'error' = 'error';
  showSignupAction = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      username: ['', Validators.required],
      password: ['', Validators.required]
    });
  }

  login() {
    this.loginMessage = '';
    this.showSignupAction = false;

    if (this.loginForm.valid) {
      this.authService.login(this.loginForm.value)
        .subscribe({
          next: (res: any) => {
            if (res?.token) {
              this.authService.saveToken(res.token);
              this.loginMessageType = 'success';
              this.loginMessage = 'Login successful. Redirecting...';
              this.router.navigate(['/upload']);
              return;
            }

            this.loginMessageType = 'error';
            const responseMessage = this.getErrorMessage(res);
            if (this.isUserNotFound(responseMessage)) {
              this.loginMessage = 'User does not exist. Redirecting to signup...';
              this.showSignupAction = true;
              setTimeout(() => this.router.navigate(['/signup']), 1200);
              return;
            }

            this.loginMessage = responseMessage || 'User not found or invalid credentials.';
          },
          error: (error: HttpErrorResponse) => {
            this.loginMessageType = 'error';
            const backendMessage = this.getErrorMessage(error?.error) || this.getErrorMessage(error?.message);

            if (error.status === 404 || this.isUserNotFound(backendMessage)) {
              this.loginMessage = 'User does not exist. Redirecting to signup...';
              this.showSignupAction = true;
              setTimeout(() => this.router.navigate(['/signup']), 1200);
              return;
            }

            if (error.status === 401) {
              this.loginMessage = 'Invalid username or password.';
              this.showSignupAction = false;
              return;
            }

            this.loginMessage = backendMessage || 'Invalid username or password.';
            this.showSignupAction = false;
          }
        });
    }
  }

  goToSignup() {
    this.router.navigate(['/signup']);
  }

  private isUserNotFound(message: string): boolean {
    const normalizedMessage = message.toLowerCase();
    return normalizedMessage.includes('not found')
      || normalizedMessage.includes('does not exist')
      || normalizedMessage.includes("doesn't exist")
      || normalizedMessage.includes('user not found')
      || normalizedMessage.includes('no user');
  }

  private getErrorMessage(payload: unknown): string {
    if (!payload) {
      return '';
    }

    if (typeof payload === 'string') {
      return payload;
    }

    if (typeof payload === 'object') {
      const data = payload as { message?: string; Message?: string; error?: string };
      return data.message || data.Message || data.error || '';
    }

    return '';
  }
}