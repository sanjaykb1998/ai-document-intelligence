import { Component } from '@angular/core';
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

    if (this.loginForm.valid) {
      this.authService.login(this.loginForm.value)
        .subscribe({
          next: (res: any) => {
            this.authService.saveToken(res.token);
            this.loginMessageType = 'success';
            this.loginMessage = 'Login successful. Redirecting...';
            this.router.navigate(['/upload']);
          },
          error: () => {
            this.loginMessageType = 'error';
            this.loginMessage = 'Invalid username or password.';
          }
        });
    }
  }
}