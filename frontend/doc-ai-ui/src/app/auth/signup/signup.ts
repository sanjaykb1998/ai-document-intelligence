import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './signup.html',
  styleUrls: ['./signup.css']
})
export class SignupComponent {
  signupForm: any;
  signupMessage = '';
  signupMessageType: 'success' | 'error' = 'error';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.signupForm = this.fb.group({
      username: ['', Validators.required],
      email: ['', Validators.required],
      password: ['', Validators.required]
    });
  }

  signup() {
    this.signupMessage = '';

    if (this.signupForm.valid) {
      this.authService.signup(this.signupForm.value)
        .subscribe({
          next: () => {
            this.signupMessageType = 'success';
            this.signupMessage = 'User created successfully. Redirecting to login...';
            setTimeout(() => this.router.navigate(['/login']), 1200);
          },
          error: (err: HttpErrorResponse) => {
            if (this.isUserAlreadyExistsError(err)) {
              this.signupMessageType = 'error';
              this.signupMessage = 'User already exists. Please login.';
              setTimeout(() => this.router.navigate(['/login']), 1200);
              return;
            }

            this.signupMessageType = 'error';
            this.signupMessage = String(err.error?.message || err.error || 'Error creating user');
          }
        });
    }
  }

  private isUserAlreadyExistsError(err: HttpErrorResponse): boolean {
    if (err.status === 409) {
      return true;
    }

    const backendMessage = String(err.error?.message || err.error || '').toLowerCase();
    return backendMessage.includes('exist') || backendMessage.includes('already');
  }
}