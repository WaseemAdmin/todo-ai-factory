import { Component } from '@angular/core';
import { TodoPage } from './features/todos/todo-page';

@Component({
  selector: 'app-root',
  imports: [TodoPage],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
}
